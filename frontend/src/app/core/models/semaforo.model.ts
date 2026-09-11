export type SemaforoColor = 'Green' | 'Yellow' | 'Red';

export interface StudentStatus {
  id: string;
  fullName: string;
  gradeLevel: string;
  avatarUrl: string;
  lastLoginDate?: string | Date;
  inactiveDays: number;
  semaforoColor: SemaforoColor;
  semaforoLabel: string;
  semaforoEmoji: string;
  description: string;
}

export interface SemaforoSummaryMetrics {
  totalStudents: number;
  greenCount: number;
  yellowCount: number;
  redCount: number;
}
