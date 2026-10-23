import { Component, Input, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PasswordPolicyService, PasswordPolicyValidationResult } from '../../../core/services/password-policy.service';

@Component({
  selector: 'app-password-policy-meter',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './password-policy-meter.component.html',
  styleUrls: ['./password-policy-meter.component.css']
})
export class PasswordPolicyMeterComponent implements OnChanges {
  @Input() password = '';
  @Input() username?: string;
  @Input() fullName?: string;
  @Input() showChecklist = true;
  @Input() compact = false;

  public result: PasswordPolicyValidationResult;

  constructor(private policyService: PasswordPolicyService) {
    this.result = this.policyService.validate('');
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['password'] || changes['username'] || changes['fullName']) {
      this.result = this.policyService.validate(this.password, this.username, this.fullName);
    }
  }
}
