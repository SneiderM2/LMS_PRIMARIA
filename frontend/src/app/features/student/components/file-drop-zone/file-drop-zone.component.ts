import { Component, EventEmitter, Input, OnDestroy, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subject, takeUntil } from 'rxjs';
import { StudentService } from '../../../../core/services/student.service';
import { TranslatePipe } from '../../../../shared/pipes/translate.pipe';

@Component({
  selector: 'app-file-drop-zone',
  standalone: true,
  imports: [CommonModule, TranslatePipe],
  templateUrl: './file-drop-zone.component.html',
  styleUrls: ['./file-drop-zone.component.css']
})
export class FileDropZoneComponent implements OnDestroy {
  @Input({ required: true }) assignmentId!: string;
  @Input() assignmentTitle: string = 'Mi Tarea';
  @Output() submitted = new EventEmitter<void>();
  @Output() submissionComplete = new EventEmitter<void>();
  @Output() cancelled = new EventEmitter<void>();

  public isDragging = false;
  public selectedFile: File | null = null;
  public isUploading = false;
  public uploadSuccess = false;
  public errorMessage: string | null = null;
  public showConfetti = false;

  private readonly destroy$ = new Subject<void>();
  private submitTimerId: ReturnType<typeof setTimeout> | null = null;
  private confettiTimerId: ReturnType<typeof setTimeout> | null = null;

  private readonly allowedTypes = [
    'application/pdf',
    'image/jpeg',
    'image/pjpeg',
    'image/jpg',
    'image/png',
    'image/x-png',
    'image/webp',
    'application/msword',
    'application/vnd.ms-word',
    'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
    'application/zip',
    'application/x-zip-compressed',
    'application/octet-stream'
  ];
  private readonly allowedExtensions = ['.pdf', '.jpg', '.jpeg', '.png', '.webp', '.doc', '.docx'];
  private readonly maxSizeBytes = 25 * 1024 * 1024; // 25MB

  constructor(private studentService: StudentService) {}

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    if (this.submitTimerId) {
      clearTimeout(this.submitTimerId);
    }
    if (this.confettiTimerId) {
      clearTimeout(this.confettiTimerId);
    }
  }

  public onDragOver(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragging = true;
  }

  public onDragLeave(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragging = false;
  }

  public onDrop(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragging = false;

    if (event.dataTransfer && event.dataTransfer.files.length > 0) {
      void this.handleFile(event.dataTransfer.files[0]);
    }
  }

  public onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      void this.handleFile(input.files[0]);
    }
  }

  private async handleFile(file: File): Promise<void> {
    this.errorMessage = null;

    if (file.size === 0) {
      this.errorMessage = 'El archivo seleccionado está vacío. Por favor elige un archivo válido.';
      return;
    }

    if (file.size > this.maxSizeBytes) {
      this.errorMessage = '¡Ups! El archivo es muy pesado. Máximo 25 MB 📦';
      return;
    }

    const extIndex = file.name.lastIndexOf('.');
    const ext = extIndex >= 0 ? file.name.substring(extIndex).toLowerCase() : '';
    if (!this.allowedExtensions.includes(ext)) {
      this.errorMessage = 'Formato no permitido. Usa archivos PDF, imágenes (JPG, PNG, WEBP) o Word (.doc, .docx).';
      return;
    }

    if (file.type && !this.allowedTypes.includes(file.type.toLowerCase())) {
      this.errorMessage = `El tipo MIME del archivo (${file.type}) no está permitido por seguridad.`;
      return;
    }

    const validSignature = await this.validateMagicBytes(file, ext);
    if (!validSignature) {
      this.errorMessage = 'El contenido binario del archivo (Magic Bytes) no coincide con su extensión. Verifica que no esté dañado o renombrado.';
      return;
    }

    this.selectedFile = file;
    this.triggerFestiveAnimation();
  }

  private async validateMagicBytes(file: File, ext: string): Promise<boolean> {
    try {
      const slice = file.slice(0, 12);
      const buffer = await slice.arrayBuffer();
      const bytes = new Uint8Array(buffer);
      if (bytes.length < 4) return false;

      const hex = Array.from(bytes)
        .map(b => b.toString(16).padStart(2, '0').toUpperCase())
        .join('');

      switch (ext) {
        case '.pdf':
          return hex.startsWith('25504446');
        case '.jpg':
        case '.jpeg':
          return hex.startsWith('FFD8FF');
        case '.png':
          return hex.startsWith('89504E470D0A1A0A');
        case '.webp':
          return hex.startsWith('52494646') && hex.substring(16, 24) === '57454250';
        case '.docx':
          return hex.startsWith('504B0304') || hex.startsWith('504B0506') || hex.startsWith('504B0708');
        case '.doc':
          return hex.startsWith('D0CF11E0A1B11AE1');
        default:
          return false;
      }
    } catch {
      return true;
    }
  }

  public removeFile(): void {
    this.selectedFile = null;
    this.errorMessage = null;
    this.showConfetti = false;
  }

  public submitAssignment(): void {
    if (!this.selectedFile || !this.assignmentId) return;

    this.isUploading = true;
    this.errorMessage = null;

    this.studentService.uploadAssignment(this.assignmentId, this.selectedFile)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (res) => {
          this.isUploading = false;
          if (res.success) {
            this.uploadSuccess = true;
            this.showConfetti = true;
            this.submitTimerId = setTimeout(() => {
              this.submitted.emit();
              this.submissionComplete.emit();
            }, 2200);
          } else {
            this.errorMessage = res.message || 'No pudimos guardar tu tarea. Intenta de nuevo.';
          }
        },
        error: (err) => {
          this.isUploading = false;
          this.errorMessage = err?.error?.message || 'Ocurrió un error al enviar tu tarea. ¡Pide ayuda a tu profe o papás!';
        }
      });
  }

  private triggerFestiveAnimation(): void {
    this.showConfetti = true;
    if (this.confettiTimerId) {
      clearTimeout(this.confettiTimerId);
    }
    this.confettiTimerId = setTimeout(() => {
      this.showConfetti = false;
    }, 4000);
  }

  public formatFileSize(bytes: number): string {
    if (bytes < 1024) return bytes + ' B';
    else if (bytes < 1048576) return (bytes / 1024).toFixed(1) + ' KB';
    else return (bytes / 1048576).toFixed(1) + ' MB';
  }
}
