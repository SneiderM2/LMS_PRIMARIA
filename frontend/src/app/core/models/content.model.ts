export type ContentType = 'Video' | 'Pdf' | 'Assignment';

export interface StudentSubmission {
  submissionId?: number;
  id: string;
  assignmentId: string;
  studentId: string;
  studentName: string;
  studentAvatarUrl?: string;
  fileUrl: string;
  originalFileName: string;
  submittedAt: string | Date;
  feedback?: string;
  grade?: number;
  status?: string;
}

export interface Content {
  id: string;
  title: string;
  description: string;
  type: ContentType;
  subject: string;
  fileUrl?: string;
  dueDate?: string | Date;
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
  gradeLevel: string;
}

export interface UploadResult {
  success: boolean;
  message: string;
  submission?: StudentSubmission;
}
