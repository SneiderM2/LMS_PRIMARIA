import { Component, Input } from '@angular/core';
import { SupportedLanguage } from '../../../core/services/language.service';

/**
 * Banderas SVG locales.
 *
 * No se usan emojis regionales porque Windows/Chrome no los renderiza como
 * banderas: muestra el par de letras del código (por ejemplo, "ES ES").
 * Al ser SVG inline no hay peticiones externas ni problemas de CSP/PWA.
 */
@Component({
  selector: 'app-language-flag',
  standalone: true,
  template: `
    @switch (code) {
      @case ('es') {
        <svg viewBox="0 0 60 40" role="img" aria-hidden="true" focusable="false">
          <rect width="60" height="40" fill="#aa151b" />
          <rect y="10" width="60" height="20" fill="#f1bf00" />
        </svg>
      }
      @case ('en') {
        <svg viewBox="0 0 60 40" role="img" aria-hidden="true" focusable="false">
          <rect width="60" height="40" fill="#ffffff" />
          <g fill="#b22234">
            <rect width="60" height="3.08" />
            <rect y="6.15" width="60" height="3.08" />
            <rect y="12.31" width="60" height="3.08" />
            <rect y="18.46" width="60" height="3.08" />
            <rect y="24.62" width="60" height="3.08" />
            <rect y="30.77" width="60" height="3.08" />
            <rect y="36.92" width="60" height="3.08" />
          </g>
          <rect width="24" height="21.54" fill="#3c3b6e" />
          <g fill="#ffffff">
            <circle cx="4" cy="4" r="1.1" />
            <circle cx="12" cy="4" r="1.1" />
            <circle cx="20" cy="4" r="1.1" />
            <circle cx="8" cy="9" r="1.1" />
            <circle cx="16" cy="9" r="1.1" />
            <circle cx="4" cy="14" r="1.1" />
            <circle cx="12" cy="14" r="1.1" />
            <circle cx="20" cy="14" r="1.1" />
            <circle cx="8" cy="19" r="1.1" />
            <circle cx="16" cy="19" r="1.1" />
          </g>
        </svg>
      }
      @case ('zh') {
        <svg viewBox="0 0 60 40" role="img" aria-hidden="true" focusable="false">
          <rect width="60" height="40" fill="#de2910" />
          <polygon
            fill="#ffde00"
            points="12,5 14.4,12.2 22,12.2 15.9,16.7 18.2,24 12,19.5 5.8,24 8.1,16.7 2,12.2 9.6,12.2" />
          <g fill="#ffde00">
            <polygon points="24,4 24.7,6 26.8,6 25.1,7.3 25.7,9.4 24,8.1 22.3,9.4 22.9,7.3 21.2,6 23.3,6" />
            <polygon points="30,9 30.7,11 32.8,11 31.1,12.3 31.7,14.4 30,13.1 28.3,14.4 28.9,12.3 27.2,11 29.3,11" />
            <polygon points="30,16 30.7,18 32.8,18 31.1,19.3 31.7,21.4 30,20.1 28.3,21.4 28.9,19.3 27.2,18 29.3,18" />
            <polygon points="24,21 24.7,23 26.8,23 25.1,24.3 25.7,26.4 24,25.1 22.3,26.4 22.9,24.3 21.2,23 23.3,23" />
          </g>
        </svg>
      }
      @case ('ja') {
        <svg viewBox="0 0 60 40" role="img" aria-hidden="true" focusable="false">
          <rect width="60" height="40" fill="#ffffff" />
          <circle cx="30" cy="20" r="12" fill="#bc002d" />
        </svg>
      }
      @case ('pt') {
        <svg viewBox="0 0 60 40" role="img" aria-hidden="true" focusable="false">
          <rect width="60" height="40" fill="#009c3b" />
          <polygon fill="#ffdf00" points="30,4 56,20 30,36 4,20" />
          <circle cx="30" cy="20" r="8.5" fill="#002776" />
          <path d="M21.7 18.2a8.5 8.5 0 0 0 16.6 1.2" fill="none" stroke="#ffffff" stroke-width="1.6" />
        </svg>
      }
    }
  `,
  styles: [`
    :host {
      display: inline-flex;
      width: 1.35rem;
      height: 1rem;
      flex: 0 0 auto;
      border-radius: 2px;
      overflow: hidden;
      line-height: 0;
      box-shadow: 0 0 0 1px rgba(15, 23, 42, 0.18);
      vertical-align: middle;
    }

    :host svg {
      display: block;
      width: 100%;
      height: 100%;
    }
  `]
})
export class LanguageFlagComponent {
  @Input({ required: true }) code!: SupportedLanguage;
}
