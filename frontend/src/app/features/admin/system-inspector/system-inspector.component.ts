import { Component, OnInit, OnDestroy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';
import { SystemFilesService, LogFileInfo, ParsedLogEntry, AuditLogItem } from '../../../core/services/system-files.service';
import { AuthService } from '../../../core/services/auth.service';
import { ConfirmDialogService } from '../../../core/services/confirm-dialog.service';
import { ToastService } from '../../../core/services/toast.service';
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';

@Component({
  selector: 'app-system-inspector',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslatePipe],
  templateUrl: './system-inspector.component.html',
  styleUrls: ['./system-inspector.component.css']
})
export class SystemInspectorComponent implements OnInit, OnDestroy {
  // Pestañas Principales: 'audit' (Auditoría Global), 'logs' (Archivos tradicionales .log)
  public activeTab = signal<'audit' | 'logs'>('audit');
  public logViewMode = signal<'friendly' | 'raw'>('friendly');
  public logFiles = signal<LogFileInfo[]>([]);
  public selectedLogName = signal<string>('app-audit.log');
  
  public currentFileName = signal<string>('app-audit.log');
  public currentContent = signal<string>('');
  public fileSize = signal<number>(0);
  public lastModified = signal<string>('');
  
  // Auditoría Global Estructurada (JSON)
  public auditLogs = signal<AuditLogItem[]>([]);
  public auditTotalCount = signal<number>(0);
  public auditPage = signal<number>(1);
  public auditPageSize = 20;
  public auditSeverity = signal<string>('ALL');
  public auditRole = signal<string>('ALL');
  public auditKeyword = signal<string>('');
  public auditFromDate = signal<string>('');
  public auditToDate = signal<string>('');
  public expandedLogId = signal<string | null>(null);
  public isAuditLoading = signal<boolean>(false);

  // Para el módulo de logs clásicos
  public parsedLogs = signal<ParsedLogEntry[]>([]);
  public selectedSeverity = signal<string>('ALL');
  public selectedDate = signal<string>('');
  public searchUser = signal<string>('');

  public isLoading = signal<boolean>(false);
  public errorMessage = signal<string>('');
  public successMessage = signal<string>('');
  public filterQuery = signal<string>('');
  public autoRefreshEnabled = signal<boolean>(false);
  public lastRefreshedAt = signal<string>('');

  private readonly destroy$ = new Subject<void>();
  private statusTimerId: ReturnType<typeof setTimeout> | null = null;
  private autoRefreshTimerId: ReturnType<typeof setInterval> | null = null;

  constructor(
    private systemFilesService: SystemFilesService,
    public authService: AuthService,
    private confirmDialogService: ConfirmDialogService,
    private toastService: ToastService
  ) {}

  ngOnInit(): void {
    this.updateLastRefreshedTimestamp();
    this.loadGlobalAudit();
    this.loadLogsList();
  }

  ngOnDestroy(): void {
    if (this.statusTimerId) {
      clearTimeout(this.statusTimerId);
      this.statusTimerId = null;
    }
    this.stopAutoRefresh();
    this.destroy$.next();
    this.destroy$.complete();
  }

  public toggleAutoRefresh(): void {
    if (this.autoRefreshEnabled()) {
      this.stopAutoRefresh();
    } else {
      this.autoRefreshEnabled.set(true);
      this.refreshCurrent();
      this.autoRefreshTimerId = setInterval(() => {
        this.refreshCurrent();
      }, 8000);
    }
  }

  private stopAutoRefresh(): void {
    this.autoRefreshEnabled.set(false);
    if (this.autoRefreshTimerId) {
      clearInterval(this.autoRefreshTimerId);
      this.autoRefreshTimerId = null;
    }
  }

  private updateLastRefreshedTimestamp(): void {
    const now = new Date();
    this.lastRefreshedAt.set(now.toLocaleTimeString('es-CO', { hour12: false }));
  }

  public get auditPageStats(): { info: number; audit: number; warning: number; error: number } {
    const items = this.auditLogs();
    return {
      info: items.filter(i => i.level === 'INFO').length,
      audit: items.filter(i => i.level === 'AUDIT').length,
      warning: items.filter(i => i.level === 'WARNING').length,
      error: items.filter(i => i.level === 'ERROR').length
    };
  }

  public get parsedLogStats(): { total: number; info: number; audit: number; warning: number; error: number } {
    const items = this.parsedLogs();
    return {
      total: items.length,
      info: items.filter(i => i.level === 'INFO').length,
      audit: items.filter(i => i.level === 'AUDIT').length,
      warning: items.filter(i => i.level === 'WARNING').length,
      error: items.filter(i => i.level === 'ERROR').length
    };
  }

  public filterAuditBySeverityChip(level: string): void {
    this.auditSeverity.set(level);
    this.applyAuditFilters();
  }

  public filterParsedBySeverityChip(level: string): void {
    this.selectedSeverity.set(level);
    this.loadParsedLogs();
  }

  // Verifica si el usuario actual tiene permisos de administración
  public isAdmin(): boolean {
    const role = this.authService.userRole()?.toLowerCase();
    return role === 'admin' || role === 'administrador' || role === '.admin';
  }

  public selectTab(tab: 'audit' | 'logs'): void {
    this.activeTab.set(tab);
    this.errorMessage.set('');
    this.successMessage.set('');
    
    if (tab === 'audit') {
      this.loadGlobalAudit();
    } else {
      if (this.selectedLogName()) {
        this.loadLog(this.selectedLogName());
      } else if (this.logFiles().length > 0) {
        this.loadLog(this.logFiles()[0].name);
      } else {
        this.loadLogsList();
      }
    }
  }

  // --- Auditoría Global ---
  public loadGlobalAudit(): void {
    this.isAuditLoading.set(true);
    this.errorMessage.set('');

    this.systemFilesService.getGlobalAuditLogs({
      page: this.auditPage(),
      pageSize: this.auditPageSize,
      level: this.auditSeverity(),
      role: this.auditRole(),
      keyword: this.auditKeyword(),
      from: this.auditFromDate(),
      to: this.auditToDate()
    })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (res) => {
          this.auditLogs.set(res.items || []);
          this.auditTotalCount.set(res.totalCount || 0);
          this.isAuditLoading.set(false);
        },
        error: (err) => {
          this.isAuditLoading.set(false);
          this.errorMessage.set('Error al consultar auditoría: ' + (err.error?.message || err.message));
        }
      });
  }

  public applyAuditFilters(): void {
    this.auditPage.set(1);
    this.loadGlobalAudit();
  }

  public resetAuditFilters(): void {
    this.auditSeverity.set('ALL');
    this.auditRole.set('ALL');
    this.auditKeyword.set('');
    this.auditFromDate.set('');
    this.auditToDate.set('');
    this.auditPage.set(1);
    this.loadGlobalAudit();
  }

  public changeAuditPage(newPage: number): void {
    if (newPage < 1) return;
    this.auditPage.set(newPage);
    this.loadGlobalAudit();
  }

  public downloadAuditFile(): void {
    this.systemFilesService.downloadAuditLogFile();
  }

  // --- Logs Clásicos ---
  public setViewMode(mode: 'friendly' | 'raw'): void {
    this.logViewMode.set(mode);
    if (mode === 'friendly') {
      this.loadParsedLogs();
    } else {
      this.loadRawLog(this.selectedLogName());
    }
  }

  public toggleAccordion(id: string): void {
    if (this.expandedLogId() === id) {
      this.expandedLogId.set(null);
    } else {
      this.expandedLogId.set(id);
    }
  }

  public loadLogsList(): void {
    this.systemFilesService.getLogsList()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (res) => {
          this.logFiles.set(res.files || []);
          if (res.files && res.files.length > 0 && !this.selectedLogName()) {
            const target = res.files.some(f => f.name === 'app-audit.log') 
              ? 'app-audit.log' 
              : res.files.some(f => f.name === 'access.log') 
                ? 'access.log' 
                : res.files[0].name;
            this.selectedLogName.set(target);
          }
        },
        error: (err) => {
          if (err.status === 403) {
            this.errorMessage.set('Acceso restringido: No tienes permisos para consultar los registros del servidor.');
          }
        }
      });
  }

  public loadLog(fileName: string): void {
    this.selectedLogName.set(fileName);
    this.currentFileName.set(fileName);

    if (this.logViewMode() === 'friendly') {
      this.loadParsedLogs();
    } else {
      this.loadRawLog(fileName);
    }
  }

  public loadParsedLogs(): void {
    const fileName = this.selectedLogName() || 'access.log';
    this.isLoading.set(true);
    this.errorMessage.set('');

    this.systemFilesService.getParsedLogs(
      fileName,
      this.selectedSeverity(),
      this.searchUser(),
      this.selectedDate()
    )
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (entries) => {
          this.parsedLogs.set(entries);
          this.isLoading.set(false);
        },
        error: (err) => {
          this.isLoading.set(false);
          this.errorMessage.set('Error al parsear el archivo de log: ' + (err.error?.message || err.message));
        }
      });
  }

  public loadRawLog(fileName: string): void {
    this.isLoading.set(true);
    this.errorMessage.set('');
    this.systemFilesService.getLogContent(fileName)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (res) => {
          this.currentContent.set(res.content);
          this.fileSize.set(res.size);
          this.lastModified.set(res.lastModified);
          this.isLoading.set(false);
        },
        error: (err) => {
          this.isLoading.set(false);
          this.errorMessage.set(`Error al leer archivo '${fileName}': ` + (err.error?.message || err.message));
        }
      });
  }

  public refreshCurrent(): void {
    this.updateLastRefreshedTimestamp();
    this.loadLogsList();
    if (this.activeTab() === 'audit') {
      this.loadGlobalAudit();
    } else {
      if (this.logViewMode() === 'friendly') {
        this.loadParsedLogs();
      } else {
        this.loadRawLog(this.selectedLogName());
      }
    }
  }

  public async clearCurrentLog(): Promise<void> {
    const fileName = this.selectedLogName();
    if (!fileName) return;

    const confirmed = await this.confirmDialogService.confirm({
      title: 'Reiniciar Registro de Actividad',
      message: '¿Estás seguro de reiniciar el registro seleccionado? Se iniciará un ciclo limpio.',
      tone: 'warning',
      confirmText: 'Reiniciar'
    });
    if (!confirmed) {
      return;
    }

    this.systemFilesService.clearLog(fileName)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (res) => {
          this.successMessage.set(res.message);
          this.toastService.success(res.message);
          this.refreshCurrent();
          if (this.statusTimerId) clearTimeout(this.statusTimerId);
          this.statusTimerId = setTimeout(() => {
            this.successMessage.set('');
            this.statusTimerId = null;
          }, 4000);
        },
        error: (err) => {
          const msg = 'Error al limpiar el registro: ' + (err.error?.message || err.message);
          this.errorMessage.set(msg);
          this.toastService.error(msg);
        }
      });
  }

  public copyToClipboard(): void {
    const textToCopy = this.activeTab() === 'audit'
      ? JSON.stringify(this.auditLogs(), null, 2)
      : (this.logViewMode() === 'raw' ? this.currentContent() : JSON.stringify(this.parsedLogs(), null, 2));

    navigator.clipboard.writeText(textToCopy).then(() => {
      this.successMessage.set('Contenido copiado al portapapeles');
      if (this.statusTimerId) clearTimeout(this.statusTimerId);
      this.statusTimerId = setTimeout(() => {
        this.successMessage.set('');
        this.statusTimerId = null;
      }, 3000);
    });
  }

  public get filteredLines(): string[] {
    const text = this.currentContent() || '';
    const lines = text.split('\n');
    const query = this.filterQuery().trim().toLowerCase();
    
    if (!query) {
      return lines;
    }
    return lines.filter(line => line.toLowerCase().includes(query));
  }

  public formatFileSize(bytes: number): string {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  }

  public getLineClass(line: string): string {
    const l = line.toUpperCase();
    if (l.includes('ERROR') || l.includes('EXCEPTION') || l.includes('HTTP 50') || l.includes('HTTP 40')) {
      return 'line-error';
    }
    if (l.includes('WARN') || l.includes('WARNING')) {
      return 'line-warning';
    }
    if (l.includes('AUDIT') || l.includes('LEY 1581') || l.includes('ENTREGA') || l.includes('CALIFICA')) {
      return 'line-audit';
    }
    if (l.includes('INFO') || l.includes('HTTP 200') || l.includes('HTTP 201')) {
      return 'line-info';
    }
    return 'line-default';
  }
}
