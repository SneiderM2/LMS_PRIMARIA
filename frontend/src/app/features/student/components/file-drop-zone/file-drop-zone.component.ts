import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { StudentService } from '../../../../core/services/student.service';

@Component({
  selector: 'app-file-drop-zone',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './file-drop-zone.component.html',
  styleUrls: ['./file-drop-zone.component.css']
})
export class FileDropZoneComponent {
  @Input({ required: true }) assignmentId!: string;
  @Input() assignmentTitle: string = 'Mi Tarea';
  @Output() submitted = new EventEmitter<void>();
  @Output() cancelled = new EventEmitter<void>();

  public isDragging = false;
  public selectedFile: File | null = null;
  public isUploading = false;
  public uploadSuccess = false;
  public errorMessage: string | null = null;
  public showConfetti = false;

  private readonly allowedTypes = [
    'application/pdf',
    'image/jpeg',
    'image/png',
    'image/webp',
    'application/msword',
    'application/vnd.openxmlformats-officedocument.wordprocessingml.document'
  ];
  private readonly maxSizeBytes = 25 * 1024 * 1024; // 25MB

  constructor(private studentService: StudentService) {}

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
      this.handleFile(event.dataTransfer.files[0]);
    }
  }

  public onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      this.handleFile(input.files[0]);
    }
  }

  private handleFile(file: File): void {
    this.errorMessage = null;

    if (file.size > this.maxSizeBytes) {
      this.errorMessage = '¡Ups! El archivo es muy pesado. Máximo 25 MB 📦';
      return;
    }

    this.selectedFile = file;
    // Disparar animación festiva de confeti al seleccionar
    this.triggerFestiveAnimation();
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

    this.studentService.uploadAssignment(this.assignmentId, this.selectedFile).subscribe({
      next: (res) => {
        this.isUploading = false;
        if (res.success) {
          this.uploadSuccess = true;
          this.showConfetti = true;
          setTimeout(() => {
            this.submitted.emit();
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
    setTimeout(() => {
      this.showConfetti = false;
    }, 4000);
  }

  public formatFileSize(bytes: number): string {
    if (bytes < 1024) return bytes + ' B';
    else if (bytes < 1048576) return (bytes / 1024).toFixed(1) + ' KB';
    else return (bytes / 1048576).toFixed(1) + ' MB';
  }
}
