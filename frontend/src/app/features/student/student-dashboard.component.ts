import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { StudentService } from '../../core/services/student.service';
import { AuthService } from '../../core/services/auth.service';
import { Content } from '../../core/models/content.model';
import { FileDropZoneComponent } from './components/file-drop-zone/file-drop-zone.component';

@Component({
  selector: 'app-student-dashboard',
  standalone: true,
  imports: [CommonModule, FileDropZoneComponent],
  templateUrl: './student-dashboard.component.html',
  styleUrls: ['./student-dashboard.component.css']
})
export class StudentDashboardComponent implements OnInit {
  public contents: Content[] = [];
  public filteredContents: Content[] = [];
  public isLoading = true;
  public selectedSubject: string = 'Todas';

  // Control del modal de entrega Drag & Drop
  public showDropZone = false;
  public activeAssignmentId: string | null = null;
  public activeAssignmentTitle: string = '';

  public readonly subjects = [
    { name: 'Todas', icon: '🌈', color: '#38bdf8' },
    { name: 'Matemáticas', icon: '📐', color: '#facc15' },
    { name: 'Ciencias Naturales', icon: '🔬', color: '#4ade80' },
    { name: 'Lengua y Literatura', icon: '📖', color: '#fb923c' },
    { name: 'Artes y Creatividad', icon: '🎨', color: '#a855f7' }
  ];

  constructor(
    public authService: AuthService,
    private studentService: StudentService
  ) {}

  ngOnInit(): void {
    this.loadDashboard();
  }

  public loadDashboard(): void {
    this.isLoading = true;
    this.studentService.getDashboard().subscribe({
      next: (items) => {
        this.contents = items;
        this.applySubjectFilter();
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
      }
    });
  }

  public selectSubject(subjectName: string): void {
    this.selectedSubject = subjectName;
    this.applySubjectFilter();
  }

  private applySubjectFilter(): void {
    if (this.selectedSubject === 'Todas') {
      this.filteredContents = this.contents;
    } else {
      this.filteredContents = this.contents.filter(c => c.subject.toLowerCase() === this.selectedSubject.toLowerCase());
    }
  }

  public openSubmissionModal(content: Content): void {
    this.activeAssignmentId = content.id;
    this.activeAssignmentTitle = content.title;
    this.showDropZone = true;
  }

  public closeSubmissionModal(): void {
    this.showDropZone = false;
    this.activeAssignmentId = null;
  }

  public onAssignmentSubmitted(): void {
    this.closeSubmissionModal();
    this.loadDashboard(); // Recargar para actualizar el estado a "Entregada"
  }

  public openMedia(url?: string): void {
    if (url) {
      window.open(url, '_blank');
    }
  }

  public logout(): void {
    this.authService.logout();
  }
}
