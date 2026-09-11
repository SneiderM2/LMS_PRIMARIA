import { Injectable, signal, computed } from '@angular/core';

export type AppTheme = 'light' | 'dark';

@Injectable({
  providedIn: 'root'
})
export class ThemeService {
  private readonly THEME_KEY = 'lms_theme';

  // Signal reactivo del tema actual ('light' | 'dark')
  public currentTheme = signal<AppTheme>(this.getInitialTheme());

  // Computed reactivo derivado sin efectos colaterales (evita error NG0600 de Angular)
  public isDarkMode = computed(() => this.currentTheme() === 'dark');

  constructor() {
    // Aplicar inmediatamente el tema inicial en el DOM
    this.applyTheme(this.currentTheme());
  }

  /**
   * Alterna entre modo claro y oscuro
   */
  public toggleTheme(): void {
    const nextTheme: AppTheme = this.currentTheme() === 'light' ? 'dark' : 'light';
    this.setTheme(nextTheme);
  }

  /**
   * Establece un tema específico y lo aplica sincrónicamente
   */
  public setTheme(theme: AppTheme): void {
    this.currentTheme.set(theme);
    this.applyTheme(theme);
    this.saveTheme(theme);
  }

  private applyTheme(theme: AppTheme): void {
    if (typeof document !== 'undefined') {
      const root = document.documentElement;
      const body = document.body;

      if (theme === 'dark') {
        root.classList.add('dark-theme');
        body.classList.add('dark-theme');
        root.setAttribute('data-theme', 'dark');
        body.setAttribute('data-theme', 'dark');
      } else {
        root.classList.remove('dark-theme');
        body.classList.remove('dark-theme');
        root.setAttribute('data-theme', 'light');
        body.setAttribute('data-theme', 'light');
      }
    }
  }

  private saveTheme(theme: AppTheme): void {
    if (typeof localStorage !== 'undefined') {
      try {
        localStorage.setItem(this.THEME_KEY, theme);
      } catch (err) {
        console.warn('No se pudo guardar la preferencia de tema en localStorage:', err);
      }
    }
  }

  private getInitialTheme(): AppTheme {
    if (typeof localStorage !== 'undefined') {
      try {
        const saved = localStorage.getItem(this.THEME_KEY) as AppTheme;
        if (saved === 'light' || saved === 'dark') {
          return saved;
        }
      } catch (err) {
        console.warn('No se pudo leer la preferencia de tema de localStorage:', err);
      }
    }

    // Si el usuario tiene preferencia por modo oscuro a nivel de sistema operativo
    if (typeof window !== 'undefined' && window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches) {
      return 'dark';
    }

    return 'light';
  }
}
