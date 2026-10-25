import { Injectable } from '@angular/core';

export interface PasswordRuleState {
  id: string;
  label: string;
  passed: boolean;
}

export interface PasswordPolicyValidationResult {
  isValid: boolean;
  length: number;
  hasLength: boolean;
  hasUpper: boolean;
  hasLower: boolean;
  hasDigit: boolean;
  hasSpecial: boolean;
  hasNoSpaces: boolean;
  hasNoPersonalData: boolean;
  distinctCount: number;
  strengthScore: number; // 0 to 100
  strengthLabel: 'Muy débil' | 'Débil' | 'Aceptable' | 'Buena' | 'Fuerte';
  strengthColor: string;
  rules: PasswordRuleState[];
  errors: string[];
}

@Injectable({
  providedIn: 'root'
})
export class PasswordPolicyService {
  public readonly minLength = 8;
  public readonly maxLength = 64;

  // Lista negra de contraseñas comunes
  private readonly blockedPasswords = new Set([
    '1234', '12345', '123456', '12345678', '123456789', '1234567890',
    'admin', 'admin123', 'docente', 'docente123', 'alumno', 'alumno123',
    'qwerty', 'password', 'clave123', 'colegio1', 'escuela1',
    'admin123!', 'docente1!', 'alumno123!', 'col2026!'
  ]);

  /**
   * Evalúa la contraseña en tiempo real contra los criterios institucionales (8 a 64 caracteres).
   */
  public validate(password: string | null | undefined, username?: string, fullName?: string): PasswordPolicyValidationResult {
    const pwd = password || '';
    const length = pwd.length;

    const hasMinLength = length >= this.minLength;
    const hasMaxLength = length <= this.maxLength;
    const hasLength = hasMinLength && hasMaxLength;
    const hasUpper = /[A-Z]/.test(pwd);
    const hasLower = /[a-z]/.test(pwd);
    const hasDigit = /[0-9]/.test(pwd);
    const hasSpecial = /[^A-Za-z0-9\s]/.test(pwd);
    const hasNoSpaces = length > 0 && !/\s/.test(pwd);

    // Detección de caracteres distintos
    const distinctChars = new Set(pwd.split('')).size;
    const hasDistinct = length === 0 || distinctChars >= Math.min(4, length);

    // No contener documento ni nombre del usuario (tokens >= 4)
    let hasNoPersonalData = true;
    if (pwd.length > 0) {
      const normalized = pwd.toLowerCase();
      const tokens: string[] = [];
      if (username && username.trim().length >= 4) {
        tokens.push(username.trim().toLowerCase().replace(/\s+/g, ''));
      }
      if (fullName) {
        const parts = fullName.split(/\s+/).map(p => p.trim().toLowerCase()).filter(p => p.length >= 4);
        tokens.push(...parts);
      }
      for (const token of tokens) {
        if (normalized.includes(token)) {
          hasNoPersonalData = false;
          break;
        }
      }
    }

    const isBlocked = this.blockedPasswords.has(pwd.toLowerCase());

    const rules: PasswordRuleState[] = [
      { id: 'length', label: `Entre ${this.minLength} y ${this.maxLength} caracteres`, passed: hasLength },
      { id: 'upper', label: 'Al menos una letra mayúscula (A-Z)', passed: hasUpper },
      { id: 'lower', label: 'Al menos una letra minúscula (a-z)', passed: hasLower },
      { id: 'digit', label: 'Al menos un número (0-9)', passed: hasDigit },
      { id: 'special', label: 'Al menos un símbolo (@, #, !, $, %, etc.)', passed: hasSpecial },
      { id: 'noSpaces', label: 'Sin espacios en blanco', passed: hasNoSpaces && length > 0 }
    ];

    if (!hasNoPersonalData) {
      rules.push({ id: 'personalData', label: 'No contiene tu nombre ni carnet', passed: false });
    }

    const passedCount = rules.filter(r => r.passed).length;
    let score = Math.round((passedCount / rules.length) * 100);

    if (length === 0) {
      score = 0;
    } else if (isBlocked) {
      score = Math.min(score, 25);
    } else if (!hasLength) {
      // La longitud es un requisito eliminatorio: no debe mostrarse "Buena"
      // solo porque las demás reglas se cumplen.
      score = Math.min(score, 34);
    }

    let strengthLabel: 'Muy débil' | 'Débil' | 'Aceptable' | 'Buena' | 'Fuerte' = 'Muy débil';
    let strengthColor = '#ef4444'; // rojo

    if (score >= 100 && !isBlocked && hasNoPersonalData) {
      strengthLabel = 'Fuerte';
      strengthColor = '#16a34a'; // verde
    } else if (score >= 80) {
      strengthLabel = 'Buena';
      strengthColor = '#0284c7'; // azul
    } else if (score >= 60) {
      strengthLabel = 'Aceptable';
      strengthColor = '#eab308'; // amarillo
    } else if (score >= 35) {
      strengthLabel = 'Débil';
      strengthColor = '#f97316'; // naranja
    }

    const errors: string[] = [];
    if (length > 0) {
      if (!hasMinLength) errors.push(`La contraseña debe tener mínimo ${this.minLength} caracteres.`);
      if (!hasMaxLength) errors.push(`La contraseña no puede superar ${this.maxLength} caracteres.`);
      if (!hasNoSpaces) errors.push('No debe contener espacios en blanco.');
      if (!hasUpper) errors.push('Debe incluir al menos una letra mayúscula.');
      if (!hasLower) errors.push('Debe incluir al menos una letra minúscula.');
      if (!hasDigit) errors.push('Debe incluir al menos un número.');
      if (!hasSpecial) errors.push('Debe incluir al menos un símbolo especial (@, #, !, etc.).');
      if (isBlocked) errors.push('Es una contraseña de uso común, elige una más segura.');
      if (!hasNoPersonalData) errors.push('No debe incluir tu nombre ni tu documento.');
    }

    const isValid = hasLength && hasUpper && hasLower && hasDigit && hasSpecial && hasNoSpaces && hasDistinct && hasNoPersonalData && !isBlocked;

    return {
      isValid,
      length,
      hasLength,
      hasUpper,
      hasLower,
      hasDigit,
      hasSpecial,
      hasNoSpaces,
      hasNoPersonalData,
      distinctCount: distinctChars,
      strengthScore: score,
      strengthLabel,
      strengthColor,
      rules,
      errors
    };
  }

  /**
   * Genera una contraseña segura dentro del rango de 8 a 64 caracteres (por defecto 12).
   */
  public generateSecurePassword(length = 12): string {
    const targetLength = Math.max(this.minLength, Math.min(this.maxLength, length));
    const uppers = 'ABCDEFGHJKLMNPQRSTUVWXYZ';
    const lowers = 'abcdefghijkmnopqrstuvwxyz';
    const digits = '23456789';
    const specials = '!@#$%&*+-_?';
    const all = uppers + lowers + digits + specials;

    const buffer: string[] = [
      uppers[this.getRandomInt(uppers.length)],
      lowers[this.getRandomInt(lowers.length)],
      digits[this.getRandomInt(digits.length)],
      specials[this.getRandomInt(specials.length)]
    ];

    while (buffer.length < targetLength) {
      buffer.push(all[this.getRandomInt(all.length)]);
    }

    // Fisher-Yates shuffle
    for (let i = buffer.length - 1; i > 0; i--) {
      const j = this.getRandomInt(i + 1);
      const temp = buffer[i];
      buffer[i] = buffer[j];
      buffer[j] = temp;
    }

    return buffer.join('');
  }

  private getRandomInt(max: number): number {
    if (typeof window !== 'undefined' && window.crypto && window.crypto.getRandomValues) {
      const array = new Uint32Array(1);
      window.crypto.getRandomValues(array);
      return array[0] % max;
    }
    return Math.floor(Math.random() * max);
  }
}
