import { Component, ChangeDetectionStrategy, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface DonutSegment {
  label: string;
  value: number;
  color: string;
}

interface ComputedArc {
  label: string;
  value: number;
  color: string;
  percentage: number;
  dashArray: string;
  dashOffset: number;
}

@Component({
  selector: 'app-donut-chart',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="donut-wrapper" role="img" [attr.aria-label]="'Gráfico de dona: ' + centerLabel">
      <div class="donut-visual" [style.width.px]="size" [style.height.px]="size">
        <svg [attr.viewBox]="'0 0 ' + size + ' ' + size" class="donut-svg">
          <!-- Background track circle -->
          <circle
            [attr.cx]="center"
            [attr.cy]="center"
            [attr.r]="radius"
            fill="transparent"
            stroke="#e2e8f0"
            [attr.stroke-width]="strokeWidth" />

          <!-- Colored Segments -->
          <circle
            *ngFor="let arc of computedArcs"
            [attr.cx]="center"
            [attr.cy]="center"
            [attr.r]="radius"
            fill="transparent"
            [attr.stroke]="arc.color"
            [attr.stroke-width]="strokeWidth"
            [attr.stroke-dasharray]="arc.dashArray"
            [attr.stroke-dashoffset]="arc.dashOffset"
            stroke-linecap="round"
            class="donut-segment">
            <title>{{ arc.label }}: {{ arc.value }} ({{ arc.percentage | number:'1.0-1' }}%)</title>
          </circle>
        </svg>

        <!-- Center Label -->
        <div class="donut-center-content">
          <span *ngIf="centerValue" class="donut-center-val">{{ centerValue }}</span>
          <span *ngIf="centerLabel" class="donut-center-lbl">{{ centerLabel }}</span>
        </div>
      </div>

      <!-- Legend -->
      <div class="donut-legend">
        <div *ngFor="let arc of computedArcs" class="legend-item">
          <span class="legend-bullet" [style.background-color]="arc.color"></span>
          <span class="legend-text">{{ arc.label }}</span>
          <strong class="legend-val">{{ arc.value }}</strong>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .donut-wrapper {
      display: flex;
      align-items: center;
      gap: 1.5rem;
      flex-wrap: wrap;
    }
    .donut-visual {
      position: relative;
      flex-shrink: 0;
    }
    .donut-svg {
      transform: rotate(-90deg);
      width: 100%;
      height: 100%;
    }
    .donut-segment {
      transition: stroke-dashoffset 0.5s ease;
    }
    .donut-center-content {
      position: absolute;
      inset: 0;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      text-align: center;
      pointer-events: none;
    }
    .donut-center-val {
      font-family: var(--font-heading, sans-serif);
      font-size: 1.35rem;
      font-weight: 700;
      color: #0f172a;
      line-height: 1.1;
    }
    .donut-center-lbl {
      font-size: 0.75rem;
      color: #64748b;
      font-weight: 600;
    }
    .donut-legend {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
      flex: 1;
      min-width: 130px;
    }
    .legend-item {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      font-size: 0.85rem;
      color: #334155;
    }
    .legend-bullet {
      width: 10px;
      height: 10px;
      border-radius: 50%;
      flex-shrink: 0;
    }
    .legend-text {
      flex: 1;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }
    .legend-val {
      font-weight: 700;
      color: #0f172a;
    }
    :host-context(body.dark-theme) .donut-center-val,
    :host-context(body.dark-mode) .donut-center-val,
    body.dark-theme .donut-center-val,
    body.dark-mode .donut-center-val,
    :host-context(body.dark-theme) .legend-val,
    :host-context(body.dark-mode) .legend-val,
    body.dark-theme .legend-val,
    body.dark-mode .legend-val {
      color: #f8fafc;
    }
    :host-context(body.dark-theme) .legend-item,
    :host-context(body.dark-mode) .legend-item,
    body.dark-theme .legend-item,
    body.dark-mode .legend-item {
      color: #cbd5e1;
    }
  `]
})
export class DonutChartComponent {
  @Input() segments: DonutSegment[] = [];
  @Input() size: number = 160;
  @Input() centerLabel: string = '';
  @Input() centerValue: string = '';

  public get strokeWidth(): number {
    return Math.round(this.size * 0.15);
  }

  public get center(): number {
    return this.size / 2;
  }

  public get radius(): number {
    return (this.size - this.strokeWidth) / 2;
  }

  public get circumference(): number {
    return 2 * Math.PI * this.radius;
  }

  public get total(): number {
    return this.segments.reduce((acc, s) => acc + (s.value || 0), 0);
  }

  public get computedArcs(): ComputedArc[] {
    const total = this.total;
    if (total === 0) return [];

    let currentOffset = 0;
    return this.segments.map(s => {
      const percentage = (s.value / total) * 100;
      const arcLength = (s.value / total) * this.circumference;
      const dashArray = `${arcLength} ${this.circumference - arcLength}`;
      const dashOffset = -currentOffset;
      currentOffset += arcLength;

      return {
        label: s.label,
        value: s.value,
        color: s.color,
        percentage,
        dashArray,
        dashOffset
      };
    });
  }
}
