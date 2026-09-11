import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ThemeService } from '../../../core/services/theme.service';

@Component({
  selector: 'app-theme-toggle',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './theme-toggle.component.html',
  styleUrls: ['./theme-toggle.component.css']
})
export class ThemeToggleComponent {
  /**
   * Si es true, el botón se posiciona fijo/flotante en la esquina inferior derecha.
   * Si es false, se comporta como un elemento inline para barras de navegación/encabezados.
   */
  @Input() floating: boolean = false;

  constructor(public themeService: ThemeService) {}

  public toggle(): void {
    this.themeService.toggleTheme();
  }
}
