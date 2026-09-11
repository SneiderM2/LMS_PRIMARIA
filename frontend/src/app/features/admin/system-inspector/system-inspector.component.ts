import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SystemFilesService, LogFileInfo } from '../../../core/services/system-files.service';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-system-inspector',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './system-inspector.component.html',
  styleUrls: ['./system-inspector.component.css']
})
export class SystemInspectorComponent implements OnInit {
  public activeTab = signal<'htaccess' | 'logs'>('htaccess');
  public logFiles = signal<LogFileInfo[]>([]);
  public selectedLogName = signal<string>('');
  
  public currentFileName = signal<string>('.htaccess');
  public currentContent = signal<string>('');
  public fileSize = signal<number>(0);
  public lastModified = signal<string>('');
  
  public isLoading = signal<boolean>(false);
  public errorMessage = signal<string>('');
  public successMessage = signal<string>('');
  public filterQuery = signal<string>('');
  public autoRefresh = signal<boolean>(false);

  private autoRefreshTimer: any = null;

  constructor(
    private systemFilesService: SystemFilesService,
    public authService: AuthService
  ) {}

  ngOnInit(): void {
    this.loadHtaccess();
    this.loadLogsList();
  }

  // Verifica si el usuario actual tiene permisos de administración
  public isAdmin(): boolean {
    const role = this.authService.userRole()?.toLowerCase();
    return role === 'admin' || role === 'administrador' || role === '.admin';
  }

  public selectTab(tab: 'htaccess' | 'logs'): void {
    this.activeTab.set(tab);
    this.errorMessage.set('');
    this.successMessage.set('');
    
    if (tab === 'htaccess') {
      this.loadHtaccess();
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

  public loadHtaccess(): void {
    this.isLoading.set(true);
    this.errorMessage.set('');
    this.systemFilesService.getHtaccess().subscribe({
      next: (res) => {
        this.currentFileName.set(res.fileName);
        this.currentContent.set(res.content);
        this.fileSize.set(res.size);
        this.lastModified.set(res.lastModified);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.isLoading.set(false);
        if (err.status === 403) {
          this.errorMessage.set('Acceso denegado (403): Tu rol no posee privilegios de administrador (.admin).');
        } else if (err.status === 404) {
          this.errorMessage.set('El archivo .htaccess no fue encontrado en el servidor.');
        } else {
          this.errorMessage.set('Error al recuperar .htaccess: ' + (err.error?.message || err.message));
        }
      }
    });
  }

  public loadLogsList(): void {
    this.systemFilesService.getLogsList().subscribe({
      next: (res) => {
        this.logFiles.set(res.files || []);
        // Si estamos en la pestaña logs y no hay seleccionado, cargar el primero
        if (this.activeTab() === 'logs' && !this.selectedLogName() && res.files?.length > 0) {
          this.loadLog(res.files[0].name);
        }
      },
      error: (err) => {
        if (err.status === 403) {
          this.errorMessage.set('Acceso restringido: No tienes permisos para consultar los registros del sistema.');
        }
      }
    });
  }

  public loadLog(fileName: string): void {
    this.isLoading.set(true);
    this.errorMessage.set('');
    this.successMessage.set('');
    this.selectedLogName.set(fileName);

    this.systemFilesService.getLogContent(fileName).subscribe({
      next: (res) => {
        this.currentFileName.set(res.fileName);
        this.currentContent.set(res.content);
        this.fileSize.set(res.size);
        this.lastModified.set(res.lastModified);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.isLoading.set(false);
        if (err.status === 403) {
          this.errorMessage.set('Acceso denegado (403): Se requiere rol de administrador (.admin).');
        } else {
          this.errorMessage.set(`Error al leer archivo '${fileName}': ` + (err.error?.message || err.message));
        }
      }
    });
  }

  public refreshCurrent(): void {
    if (this.activeTab() === 'htaccess') {
      this.loadHtaccess();
    } else {
      this.loadLogsList();
      if (this.selectedLogName()) {
        this.loadLog(this.selectedLogName());
      }
    }
  }

  public clearCurrentLog(): void {
    const fileName = this.selectedLogName();
    if (!fileName) return;

    if (!confirm(`¿Estás seguro de reiniciar el archivo de log '${fileName}'?`)) {
      return;
    }

    this.systemFilesService.clearLog(fileName).subscribe({
      next: (res) => {
        this.successMessage.set(res.message);
        this.loadLog(fileName);
        this.loadLogsList();
        setTimeout(() => this.successMessage.set(''), 4000);
      },
      error: (err) => {
        this.errorMessage.set('Error al limpiar el log: ' + (err.error?.message || err.message));
      }
    });
  }

  public copyToClipboard(): void {
    navigator.clipboard.writeText(this.currentContent()).then(() => {
      this.successMessage.set('Contenido copiado al portapapeles');
      setTimeout(() => this.successMessage.set(''), 3000);
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
    if (l.includes('INFO') || l.includes('HTTP 200')) {
      return 'line-info';
    }
    if (l.includes('DEBUG')) {
      return 'line-debug';
    }
    return 'line-default';
  }
}
