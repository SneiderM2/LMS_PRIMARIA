import { Component, ChangeDetectionStrategy, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface CalendarEvent {
  id: string | number;
  date: string | Date;
  title: string;
  color?: string;
  subtitle?: string;
}

export interface CalendarDay {
  date: Date;
  dayNumber: number;
  isCurrentMonth: boolean;
  isToday: boolean;
  events: CalendarEvent[];
}

@Component({
  selector: 'app-month-calendar',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './month-calendar.component.html',
  styleUrls: ['./month-calendar.component.css']
})
export class MonthCalendarComponent {
  @Input() events: CalendarEvent[] = [];
  @Output() eventClick = new EventEmitter<CalendarEvent>();
  @Output() dayClick = new EventEmitter<Date>();

  public currentDate: Date = new Date();
  public readonly weekDays: string[] = ['Lun', 'Mar', 'Mié', 'Jue', 'Vie', 'Sáb', 'Dom'];

  public readonly monthNames: string[] = [
    'Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio',
    'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'
  ];

  public get monthTitle(): string {
    return `${this.monthNames[this.currentDate.getMonth()]} ${this.currentDate.getFullYear()}`;
  }

  public get calendarDays(): CalendarDay[] {
    const year = this.currentDate.getFullYear();
    const month = this.currentDate.getMonth();

    const firstDayOfMonth = new Date(year, month, 1);
    const lastDayOfMonth = new Date(year, month + 1, 0);

    // Monday as 0: (getDay() + 6) % 7
    const startOffset = (firstDayOfMonth.getDay() + 6) % 7;
    const totalDaysInMonth = lastDayOfMonth.getDate();

    const today = new Date();
    const isSameDay = (d1: Date, d2: Date) =>
      d1.getFullYear() === d2.getFullYear() &&
      d1.getMonth() === d2.getMonth() &&
      d1.getDate() === d2.getDate();

    const days: CalendarDay[] = [];

    // Days from previous month
    const prevMonthLastDay = new Date(year, month, 0).getDate();
    for (let i = startOffset - 1; i >= 0; i--) {
      const date = new Date(year, month - 1, prevMonthLastDay - i);
      days.push({
        date,
        dayNumber: date.getDate(),
        isCurrentMonth: false,
        isToday: isSameDay(date, today),
        events: this.getEventsForDate(date)
      });
    }

    // Days of current month
    for (let d = 1; d <= totalDaysInMonth; d++) {
      const date = new Date(year, month, d);
      days.push({
        date,
        dayNumber: d,
        isCurrentMonth: true,
        isToday: isSameDay(date, today),
        events: this.getEventsForDate(date)
      });
    }

    // Days for next month to complete the 35 or 42 grid
    const remaining = (7 - (days.length % 7)) % 7;
    for (let d = 1; d <= remaining; d++) {
      const date = new Date(year, month + 1, d);
      days.push({
        date,
        dayNumber: d,
        isCurrentMonth: false,
        isToday: isSameDay(date, today),
        events: this.getEventsForDate(date)
      });
    }

    return days;
  }

  public prevMonth(): void {
    this.currentDate = new Date(this.currentDate.getFullYear(), this.currentDate.getMonth() - 1, 1);
  }

  public nextMonth(): void {
    this.currentDate = new Date(this.currentDate.getFullYear(), this.currentDate.getMonth() + 1, 1);
  }

  public goToToday(): void {
    this.currentDate = new Date();
  }

  public onDaySelect(day: CalendarDay): void {
    this.dayClick.emit(day.date);
  }

  public onEventSelect(event: CalendarEvent, e: MouseEvent): void {
    e.stopPropagation();
    this.eventClick.emit(event);
  }

  private getEventsForDate(date: Date): CalendarEvent[] {
    return this.events.filter(ev => {
      const evDate = typeof ev.date === 'string' ? new Date(ev.date) : ev.date;
      return (
        evDate.getFullYear() === date.getFullYear() &&
        evDate.getMonth() === date.getMonth() &&
        evDate.getDate() === date.getDate()
      );
    });
  }
}
