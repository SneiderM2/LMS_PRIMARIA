export type ContentType = 'Video' | 'Pdf' | 'Assignment';

export interface StudentSubmission {
  submissionId?: number;
  id: string;
  assignmentId: string;
  studentId: string;
  studentUsername?: string;
  studentName: string;
  studentAvatarUrl?: string;
  fileUrl: string;
  originalFileName: string;
  fileSize?: number;
  studentComment?: string;
  submittedAt: string | Date;
  gradedAt?: string | Date;
  feedback?: string;
  grade?: number;
  maxScore?: number;
  dueDate?: string | Date;
  isLate?: boolean;
  status?: string;
}

export interface Content {
  id: string;
  realId?: number;
  courseId?: number;
  title: string;
  description: string;
  type: ContentType;
  subject: string;
  fileUrl?: string;
  dueDate?: string | Date;
  maxScore?: number;
  gradeLevel: string;
  createdByUserId: string;
  createdByUserName: string;
  createdAt: string | Date;
  hasSubmitted?: boolean;
  mySubmission?: StudentSubmission;
}

export interface CreateContent {
  title: string;
  description: string;
  type: ContentType;
  subject: string;
  fileUrl?: string;
  dueDate?: string;
  maxScore?: number;
  courseId?: number;
  gradeLevel: string;
}

export interface UploadResult {
  success: boolean;
  message: string;
  submission?: StudentSubmission;
}
