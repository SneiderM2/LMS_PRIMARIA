import { Component, ChangeDetectionStrategy, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface BarChartItem {
  label: string;
  value: number;
  color?: string;
}

@Component({
  selector: 'app-mini-bar-chart',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="chart-container" [style.height.px]="height" role="img" [attr.aria-label]="ariaLabel">
      <svg *ngIf="data.length > 0" class="bar-chart-svg" viewBox="0 0 400 160" preserveAspectRatio="none">
        <defs>
          <linearGradient id="barGradDefault" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stop-color="#38bdf8" />
            <stop offset="100%" stop-color="#0284c7" />
          </linearGradient>
        </defs>

        <!-- Baseline -->
        <line x1="20" y1="130" x2="390" y2="130" stroke="#cbd5e1" stroke-width="1" stroke-dasharray="2,2" />

        <!-- Bars -->
        <g *ngFor="let item of data; let i = index">
          <rect
            [attr.x]="getBarX(i)"
            [attr.y]="getBarY(item.value)"
            [attr.width]="barWidth"
            [attr.height]="getBarHeight(item.value)"
            [attr.fill]="item.color || 'url(#barGradDefault)'"
            rx="4"
            class="bar-rect">
            <title>{{ item.label }}: {{ item.value }}{{ valueSuffix }}</title>
          </rect>
          <!-- Value Label -->
          <text
            *ngIf="item.value > 0"
            [attr.x]="getBarX(i) + barWidth / 2"
            [attr.y]="getBarY(item.value) - 6"
            text-anchor="middle"
            class="bar-value-text">
            {{ item.value }}{{ valueSuffix }}
          </text>
          <!-- Category Label -->
          <text
            [attr.x]="getBarX(i) + barWidth / 2"
            y="148"
            text-anchor="middle"
            class="bar-category-text">
            {{ item.label }}
          </text>
        </g>
      </svg>

      <!-- Accessible fallback table for screen readers -->
      <table class="sr-only">
        <caption>{{ ariaLabel }}</caption>
        <thead>
          <tr><th>Categoría</th><th>Valor</th></tr>
        </thead>
        <tbody>
          <tr *ngFor="let item of data">
            <td>{{ item.label }}</td>
            <td>{{ item.value }}{{ valueSuffix }}</td>
          </tr>
        </tbody>
      </table>
    </div>
  `,
  styles: [`
    .chart-container {
      width: 100%;
      position: relative;
    }
    .bar-chart-svg {
      width: 100%;
      height: 100%;
      overflow: visible;
    }
    .bar-rect {
      transition: y 0.3s ease, height 0.3s ease, opacity 0.2s;
    }
    .bar-rect:hover {
      opacity: 0.85;
      cursor: pointer;
    }
    .bar-value-text {
      font-size: 11px;
      font-weight: 700;
      fill: #475569;
      font-family: inherit;
    }
    .bar-category-text {
      font-size: 11px;
      fill: #64748b;
      font-family: inherit;
    }
    :host-context(body.dark-theme) .bar-value-text,
    :host-context(body.dark-mode) .bar-value-text {
      fill: #cbd5e1;
    }
    :host-context(body.dark-theme) .bar-category-text,
    :host-context(body.dark-mode) .bar-category-text {
      fill: #94a3b8;
    }
    .sr-only {
      position: absolute;
      width: 1px;
      height: 1px;
      padding: 0;
      margin: -1px;
      overflow: hidden;
      clip: rect(0, 0, 0, 0);
      white-space: nowrap;
      border-width: 0;
    }
  `]
})
export class MiniBarChartComponent {
  @Input() data: BarChartItem[] = [];
  @Input() height: number = 160;
  @Input() valueSuffix: string = '';
  @Input() ariaLabel: string = 'Gráfico de barras';

  private readonly maxChartHeight = 105;
  private readonly baselineY = 130;

  public get barWidth(): number {
    if (this.data.length === 0) return 20;
    const availableWidth = 360;
    const computed = (availableWidth / this.data.length) * 0.65;
    return Math.max(12, Math.min(36, computed));
  }

  public getBarX(index: number): number {
    if (this.data.length === 0) return 0;
    const totalW = 360;
    const slot = totalW / this.data.length;
    return 25 + index * slot + (slot - this.barWidth) / 2;
  }

  public getBarY(value: number): number {
    return this.baselineY - this.getBarHeight(value);
  }

  public getBarHeight(value: number): number {
    const maxVal = Math.max(1, ...this.data.map(d => d.value));
    return (value / maxVal) * this.maxChartHeight;
  }
}
