import { Component, ChangeDetectionStrategy, Input, Output, EventEmitter, ElementRef, HostListener, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { StudentNotification } from '../../../core/services/student.service';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-notification-bell',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './notification-bell.component.html',
  styleUrls: ['./notification-bell.component.css']
})
export class NotificationBellComponent implements OnInit {
  @Input() items: StudentNotification[] = [];
  @Output() itemClick = new EventEmitter<StudentNotification>();

  public isOpen = false;
  private readIds: Set<string> = new Set<string>();

  constructor(
    private authService: AuthService,
    private elRef: ElementRef
  ) {}

  ngOnInit(): void {
    this.loadReadIds();
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!this.elRef.nativeElement.contains(event.target)) {
      this.isOpen = false;
    }
  }

  public toggleDropdown(): void {
    this.isOpen = !this.isOpen;
  }

  public get unreadCount(): number {
    return this.items.filter(item => !this.readIds.has(item.id)).length;
  }

  public isRead(id: string): boolean {
    return this.readIds.has(id);
  }

  public markAllAsRead(): void {
    this.items.forEach(item => this.readIds.add(item.id));
    this.saveReadIds();
  }

  public onSelectNotification(item: StudentNotification): void {
    this.readIds.add(item.id);
    this.saveReadIds();
    this.itemClick.emit(item);
    this.isOpen = false;
  }

  public getTypeIcon(type: string): string {
    switch (type) {
      case 'NEW_TASK': return '📝';
      case 'GRADED': return '⭐';
      case 'DUE_SOON': return '⏰';
      case 'OVERDUE': return '⚠️';
      default: return '🔔';
    }
  }

  public formatRelativeTime(dateStr: string): string {
    if (!dateStr) return '';
    const now = Date.now();
    const time = new Date(dateStr).getTime();
    const diffSec = Math.floor((now - time) / 1000);

    if (diffSec < 60) return 'hace un momento';
    const diffMin = Math.floor(diffSec / 60);
    if (diffMin < 60) return `hace ${diffMin} min`;
    const diffHours = Math.floor(diffMin / 60);
    if (diffHours < 24) return `hace ${diffHours} h`;
    const diffDays = Math.floor(diffHours / 24);
    if (diffDays === 1) return 'ayer';
    if (diffDays < 7) return `hace ${diffDays} d`;
    return new Date(dateStr).toLocaleDateString('es-CO');
  }

  private get storageKey(): string {
    const userId = this.authService.currentUser()?.id || 'anon';
    return `scikids_read_notifications_${userId}`;
  }

  private loadReadIds(): void {
    try {
      const stored = localStorage.getItem(this.storageKey);
      if (stored) {
        this.readIds = new Set<string>(JSON.parse(stored));
      }
    } catch {
      this.readIds = new Set<string>();
    }
  }

  private saveReadIds(): void {
    try {
      localStorage.setItem(this.storageKey, JSON.stringify(Array.from(this.readIds)));
    } catch {
      // Ignore storage errors
    }
  }
}
