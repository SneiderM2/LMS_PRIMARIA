import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Content, UploadResult } from '../models/content.model';

@Injectable({
  providedIn: 'root'
})
export class StudentService {
  private readonly API_URL = 'http://localhost:5000/api/student';

  constructor(private http: HttpClient) {}

  public getDashboard(): Observable<Content[]> {
    return this.http.get<Content[]>(`${this.API_URL}/dashboard`);
  }

  public uploadAssignment(assignmentId: string, file: File): Observable<UploadResult> {
    const formData = new FormData();
    formData.append('assignmentId', assignmentId);
    formData.append('file', file);

    return this.http.post<UploadResult>(`${this.API_URL}/upload-assignment`, formData);
  }
}
