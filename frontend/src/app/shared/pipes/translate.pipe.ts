import { Pipe, PipeTransform, inject } from '@angular/core';
import { LanguageService } from '../../core/services/language.service';

@Pipe({
  name: 'translate',
  standalone: true,
  pure: false // Reactivo al cambio del signal de idioma en tiempo real
})
export class TranslatePipe implements PipeTransform {
  private languageService = inject(LanguageService);

  transform(key: string, fallback?: string): string {
    return this.languageService.translate(key, fallback);
  }
}
