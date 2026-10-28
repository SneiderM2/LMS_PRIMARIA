import { TestBed } from '@angular/core/testing';
import { ThemeService } from './theme.service';

describe('ThemeService & Color-Scheme Synchronization Tests', () => {
  let service: ThemeService;
  let metaTag: HTMLMetaElement;

  beforeEach(() => {
    localStorage.clear();

    // Asegurar que exista <meta name="color-scheme"> en el head del documento de prueba
    let existing = document.querySelector('meta[name="color-scheme"]') as HTMLMetaElement;
    if (!existing) {
      existing = document.createElement('meta');
      existing.setAttribute('name', 'color-scheme');
      existing.setAttribute('content', 'light dark');
      document.head.appendChild(existing);
    }
    metaTag = existing;

    TestBed.configureTestingModule({
      providers: [ThemeService]
    });

    service = TestBed.inject(ThemeService);
  });

  afterEach(() => {
    localStorage.clear();
  });

  it('sincroniza <meta name="color-scheme"> y atributos data-theme al cambiar a modo oscuro', () => {
    service.setTheme('dark');

    expect(service.isDarkMode()).toBeTrue();
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
    expect(metaTag.getAttribute('content')).toBe('dark');
    expect(localStorage.getItem('lms_theme')).toBe('dark');
  });

  it('alterna correctamente entre modo claro y oscuro con toggleTheme()', () => {
    service.setTheme('light');
    expect(service.isDarkMode()).toBeFalse();
    expect(metaTag.getAttribute('content')).toBe('light');

    service.toggleTheme();
    expect(service.isDarkMode()).toBeTrue();
    expect(metaTag.getAttribute('content')).toBe('dark');
  });
});
