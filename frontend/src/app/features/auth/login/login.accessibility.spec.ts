import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { LoginComponent } from './login.component';

describe('LoginComponent Accessibility (WCAG 2.1 AA) & CSP Security Tests', () => {
  let component: LoginComponent;
  let fixture: ComponentFixture<LoginComponent>;

  beforeEach(async () => {
    localStorage.clear();

    await TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([])
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('no contiene enlaces inseguros con pseudo-protocolo javascript:void(0) (cumplimiento CSP)', () => {
    const anchors = Array.from(fixture.nativeElement.querySelectorAll('a')) as HTMLAnchorElement[];
    const jsLinks = anchors.filter(a => (a.getAttribute('href') || '').toLowerCase().startsWith('javascript:'));
    expect(jsLinks.length).toBe(0);
  });

  it('utiliza botones nativos accesibles para alternar entre Iniciar Sesión y Registro (WCAG 4.1.2)', () => {
    const inlineButtons = Array.from(fixture.nativeElement.querySelectorAll('button.btn-inline-link')) as HTMLButtonElement[];
    expect(inlineButtons.length).toBe(2);

    // Simular clic para cambiar a la pestaña de registro
    inlineButtons[0].click();
    fixture.detectChanges();
    expect(component.activeTab).toBe('register');
  });

  it('expone estados ARIA válidos en las pestañas de autenticación (role="tablist" y aria-selected)', () => {
    const tabs = Array.from(fixture.nativeElement.querySelectorAll('[role="tab"]')) as HTMLElement[];
    expect(tabs.length).toBe(2);
    expect(tabs[0].getAttribute('aria-selected')).toBe('true');
    expect(tabs[1].getAttribute('aria-selected')).toBe('false');

    component.switchTab('register');
    fixture.detectChanges();

    expect(tabs[0].getAttribute('aria-selected')).toBe('false');
    expect(tabs[1].getAttribute('aria-selected')).toBe('true');
  });

  it('asocia etiquetas accesibles a todos los campos de entrada visibles', () => {
    const inputs = Array.from(
      fixture.nativeElement.querySelectorAll('input:not([type="checkbox"])')
    ) as HTMLInputElement[];

    for (const input of inputs) {
      const id = input.getAttribute('id');
      const ariaLabel = input.getAttribute('aria-label');
      const hasAssociatedLabel = id ? !!fixture.nativeElement.querySelector(`label[for="${id}"]`) : false;
      expect(hasAssociatedLabel || !!ariaLabel)
        .withContext(`El campo input#${id || 'sin-id'} debe tener <label for> o aria-label`)
        .toBeTrue();
    }
  });
});
