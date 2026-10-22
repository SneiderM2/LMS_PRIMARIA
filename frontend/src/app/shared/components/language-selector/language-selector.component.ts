import { Component, Input, inject, signal, HostListener, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LanguageService, SupportedLanguage } from '../../../core/services/language.service';

@Component({
  selector: 'app-language-selector',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div 
      class="language-selector-container notranslate skiptranslate" 
      [class.mode-floating]="mode === 'floating'"
      [class.mode-inline]="mode === 'inline'">
      
      <!-- Botón Desplegable -->
      <button 
        type="button"
        class="lang-pill-btn notranslate"
        [class.menu-open]="isOpen()"
        (click)="toggleDropdown()"
        [attr.aria-expanded]="isOpen()"
        [attr.aria-label]="'Seleccionar idioma. Idioma actual: ' + langService.currentOption().label"
        title="Cambiar idioma / Switch language">
        <span class="flag-icon">{{ langService.currentOption().flag }}</span>
        <span class="lang-code">{{ langService.currentOption().code.toUpperCase() }}</span>
        <span class="chevron-arrow" [class.rotated]="isOpen()">▾</span>
      </button>

      <!-- Menú Flotante con las 5 opciones -->
      @if (isOpen()) {
        <div class="lang-dropdown-menu animate-fade-scale notranslate skiptranslate" role="listbox">
          <div class="dropdown-header">
            <span>🌐 Idioma / Language</span>
          </div>

          @for (item of langService.languages; track item.code) {
            <button 
              type="button" 
              class="lang-option-item notranslate"
              [attr.data-lang]="item.code"
              [class.selected]="item.code === langService.currentLang()"
              (click)="onSelectLanguage(item.code)"
              role="option"
              [attr.aria-selected]="item.code === langService.currentLang()">
              <span class="option-flag">{{ item.flag }}</span>
              <div class="option-names">
                <span class="option-native">{{ item.nativeName }}</span>
                <span class="option-sub">({{ item.label }})</span>
              </div>
              @if (item.code === langService.currentLang()) {
                <span class="option-check">✓</span>
              }
            </button>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    :host {
      display: inline-block;
      font-family: inherit;
    }

    .language-selector-container {
      position: relative;
      display: inline-flex;
      align-items: center;
    }

    /* Modo flotante (esquina inferior derecha sobre el toggle de tema) */
    .language-selector-container.mode-floating {
      position: fixed;
      bottom: 5.25rem;
      right: 1.5rem;
      z-index: 99998;
    }

    .language-selector-container.mode-inline {
      position: relative;
      display: inline-flex;
    }

    .lang-pill-btn {
      display: inline-flex;
      align-items: center;
      gap: 7px;
      padding: 6px 12px;
      background: rgba(255, 255, 255, 0.88);
      backdrop-filter: blur(12px);
      -webkit-backdrop-filter: blur(12px);
      border: 1.5px solid rgba(56, 189, 248, 0.4);
      border-radius: 9999px;
      box-shadow: 0 4px 14px rgba(0, 0, 0, 0.08);
      color: #1e293b;
      font-size: 0.825rem;
      font-weight: 700;
      cursor: pointer;
      transition: all 0.22s cubic-bezier(0.34, 1.56, 0.64, 1);
      user-select: none;
      outline: none;
    }

    .lang-pill-btn:hover {
      transform: translateY(-2px);
      border-color: #0284c7;
      box-shadow: 0 6px 18px rgba(2, 132, 199, 0.18);
      background: rgba(255, 255, 255, 0.98);
    }

    .lang-pill-btn.menu-open {
      border-color: #0284c7;
      box-shadow: 0 0 0 3px rgba(56, 189, 248, 0.25);
    }

    .flag-icon {
      font-size: 1.15rem;
      line-height: 1;
    }

    .lang-code {
      letter-spacing: 0.05em;
    }

    .chevron-arrow {
      font-size: 0.85rem;
      line-height: 1;
      color: #64748b;
      transition: transform 0.2s ease;
    }

    .chevron-arrow.rotated {
      transform: rotate(180deg);
    }

    /* Menú Desplegable */
    .lang-dropdown-menu {
      position: absolute;
      top: calc(100% + 8px);
      right: 0;
      min-width: 190px;
      background: rgba(255, 255, 255, 0.95);
      backdrop-filter: blur(16px);
      -webkit-backdrop-filter: blur(16px);
      border: 1px solid rgba(226, 232, 240, 0.9);
      border-radius: 14px;
      padding: 6px;
      box-shadow: 0 12px 30px -4px rgba(0, 0, 0, 0.16), 0 4px 12px rgba(0, 0, 0, 0.08);
      display: flex;
      flex-direction: column;
      gap: 3px;
      z-index: 100000;
    }

    .language-selector-container.mode-floating .lang-dropdown-menu {
      top: auto;
      bottom: calc(100% + 8px);
    }

    .dropdown-header {
      padding: 6px 10px;
      font-size: 0.72rem;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      color: #64748b;
      border-bottom: 1px solid rgba(226, 232, 240, 0.7);
      margin-bottom: 3px;
    }

    .lang-option-item {
      display: flex;
      align-items: center;
      gap: 10px;
      width: 100%;
      padding: 8px 10px;
      border: none;
      background: transparent;
      border-radius: 8px;
      color: #1e293b;
      cursor: pointer;
      text-align: left;
      font-size: 0.85rem;
      transition: all 0.16s ease;
    }

    .lang-option-item:hover {
      background: rgba(241, 245, 249, 0.9);
      color: #0284c7;
    }

    .lang-option-item.selected {
      background: rgba(2, 132, 199, 0.12);
      color: #0284c7;
      font-weight: 700;
    }

    .option-flag {
      font-size: 1.25rem;
      line-height: 1;
    }

    .option-names {
      display: flex;
      flex-direction: column;
      line-height: 1.2;
    }

    .option-native {
      font-weight: 600;
    }

    .option-sub {
      font-size: 0.72rem;
      color: #64748b;
    }

    .option-check {
      margin-left: auto;
      font-weight: 800;
      color: #0284c7;
    }

    /* Animación */
    .animate-fade-scale {
      animation: popIn 0.18s cubic-bezier(0.16, 1, 0.3, 1);
      transform-origin: top right;
    }

    @keyframes popIn {
      from {
        opacity: 0;
        transform: scale(0.92) translateY(-6px);
      }
      to {
        opacity: 1;
        transform: scale(1) translateY(0);
      }
    }

    /* Adaptación a Modo Oscuro de la app */
    :host-context(body.dark-theme) .lang-pill-btn,
    :host-context(body.dark-mode) .lang-pill-btn {
      background: rgba(30, 41, 59, 0.88);
      border-color: rgba(56, 189, 248, 0.35);
      color: #f1f5f9;
      box-shadow: 0 4px 14px rgba(0, 0, 0, 0.3);
    }

    :host-context(body.dark-theme) .lang-pill-btn:hover,
    :host-context(body.dark-mode) .lang-pill-btn:hover {
      background: rgba(30, 41, 59, 0.98);
      border-color: #38bdf8;
      box-shadow: 0 6px 18px rgba(56, 189, 248, 0.25);
    }

    :host-context(body.dark-theme) .lang-dropdown-menu,
    :host-context(body.dark-mode) .lang-dropdown-menu {
      background: rgba(15, 23, 42, 0.96);
      border-color: rgba(51, 65, 85, 0.9);
      box-shadow: 0 12px 30px -4px rgba(0, 0, 0, 0.5);
    }

    :host-context(body.dark-theme) .dropdown-header,
    :host-context(body.dark-mode) .dropdown-header {
      color: #94a3b8;
      border-bottom-color: rgba(51, 65, 85, 0.8);
    }

    :host-context(body.dark-theme) .lang-option-item,
    :host-context(body.dark-mode) .lang-option-item {
      color: #e2e8f0;
    }

    :host-context(body.dark-theme) .lang-option-item:hover,
    :host-context(body.dark-mode) .lang-option-item:hover {
      background: rgba(30, 41, 59, 0.9);
      color: #38bdf8;
    }

    :host-context(body.dark-theme) .lang-option-item.selected,
    :host-context(body.dark-mode) .lang-option-item.selected {
      background: rgba(56, 189, 248, 0.18);
      color: #38bdf8;
    }

    :host-context(body.dark-theme) .option-sub,
    :host-context(body.dark-mode) .option-sub {
      color: #94a3b8;
    }

    :host-context(body.dark-theme) .option-check,
    :host-context(body.dark-mode) .option-check {
      color: #38bdf8;
    }

    /* Responsive */
    @media (max-width: 480px) {
      .language-selector-container.mode-floating {
        bottom: 4.85rem;
        right: 1.15rem;
      }
      .lang-pill-btn {
        padding: 5px 10px;
        font-size: 0.78rem;
      }
    }
  `]
})
export class LanguageSelectorComponent {
  @Input() mode: 'floating' | 'inline' = 'floating';

  langService = inject(LanguageService);
  private elementRef = inject(ElementRef);

  isOpen = signal(false);

  toggleDropdown(): void {
    this.isOpen.update(v => !v);
  }

  onSelectLanguage(code: SupportedLanguage): void {
    this.langService.setLanguage(code);
    this.isOpen.set(false);
  }

  @HostListener('document:click', ['$event'])
  onClickOutside(event: MouseEvent): void {
    if (!this.elementRef.nativeElement.contains(event.target)) {
      this.isOpen.set(false);
    }
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.isOpen.set(false);
  }
}
