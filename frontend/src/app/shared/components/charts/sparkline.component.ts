import { Component, ChangeDetectionStrategy, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-sparkline',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="sparkline-wrapper" [style.width.px]="width" [style.height.px]="height">
      <svg *ngIf="values.length > 1" [attr.viewBox]="'0 0 ' + width + ' ' + height" class="sparkline-svg">
        <defs>
          <linearGradient [id]="gradientId" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" [attr.stop-color]="color" stop-opacity="0.25" />
            <stop offset="100%" [attr.stop-color]="color" stop-opacity="0.0" />
          </linearGradient>
        </defs>

        <!-- Area fill -->
        <polygon [attr.points]="areaPoints" [attr.fill]="'url(#' + gradientId + ')'" />

        <!-- Line -->
        <polyline
          [attr.points]="linePoints"
          fill="none"
          [attr.stroke]="color"
          stroke-width="2"
          stroke-linecap="round"
          stroke-linejoin="round" />

        <!-- Last point dot -->
        <circle
          [attr.cx]="lastPoint.x"
          [attr.cy]="lastPoint.y"
          r="3"
          [attr.fill]="color" />
      </svg>
    </div>
  `,
  styles: [`
    .sparkline-wrapper {
      display: inline-flex;
      align-items: center;
    }
    .sparkline-svg {
      width: 100%;
      height: 100%;
      overflow: visible;
    }
  `]
})
export class SparklineComponent {
  @Input() values: number[] = [];
  @Input() color: string = '#0284c7';
  @Input() width: number = 120;
  @Input() height: number = 32;

  public readonly gradientId = `sparkGrad_${Math.random().toString(36).substring(2, 7)}`;

  private get points(): { x: number; y: number }[] {
    if (this.values.length < 2) return [];

    const min = Math.min(...this.values);
    const max = Math.max(...this.values);
    const range = max - min || 1;
    const padding = 4;
    const effH = this.height - padding * 2;
    const effW = this.width - padding * 2;

    const stepX = effW / (this.values.length - 1);

    return this.values.map((v, i) => ({
      x: padding + i * stepX,
      y: this.height - padding - ((v - min) / range) * effH
    }));
  }

  public get linePoints(): string {
    return this.points.map(p => `${p.x.toFixed(1)},${p.y.toFixed(1)}`).join(' ');
  }

  public get areaPoints(): string {
    const pts = this.points;
    if (pts.length === 0) return '';
    const first = pts[0];
    const last = pts[pts.length - 1];
    return `${first.x},${this.height} ${this.linePoints} ${last.x},${this.height}`;
  }

  public get lastPoint(): { x: number; y: number } {
    const pts = this.points;
    return pts.length > 0 ? pts[pts.length - 1] : { x: 0, y: 0 };
  }
}
