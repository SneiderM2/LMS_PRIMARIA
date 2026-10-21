import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class ExcelExportService {
  /**
   * Exporta una matriz de datos a un archivo de Excel compatible (.xls) con codificación UTF-8
   * preservando acentos, caracteres especiales y formato tabular sin dependencias externas pesadas.
   */
  public exportTable(
    fileName: string,
    sheetTitle: string,
    headers: string[],
    rows: (string | number | null | undefined)[][]
  ): void {
    const cleanFileName = fileName.endsWith('.xls') ? fileName : `${fileName}.xls`;

    let html = `
      <html xmlns:o="urn:schemas-microsoft-com:office:office" xmlns:x="urn:schemas-microsoft-com:office:excel" xmlns="http://www.w3.org/TR/REC-html40">
      <head>
        <meta http-equiv="Content-Type" content="text/html; charset=UTF-8" />
        <!--[if gte mso 9]>
        <xml>
          <x:ExcelWorkbook>
            <x:ExcelWorksheets>
              <x:ExcelWorksheet>
                <x:Name>${this.escapeXml(sheetTitle || 'Reporte')}</x:Name>
                <x:WorksheetOptions><x:DisplayGridlines/></x:WorksheetOptions>
              </x:ExcelWorksheet>
            </x:ExcelWorksheets>
          </x:ExcelWorkbook>
        </xml>
        <![endif]-->
        <style>
          th { background-color: #4f46e5; color: #ffffff; font-weight: bold; border: 1px solid #cbd5e1; padding: 6px 12px; }
          td { border: 1px solid #e2e8f0; padding: 4px 10px; font-family: sans-serif; font-size: 12px; }
          .num { mso-number-format: "0\\.0"; }
          .txt { mso-number-format: "\\@"; }
        </style>
      </head>
      <body>
        <table>
          <thead>
            <tr>
              ${headers.map(h => `<th>${this.escapeXml(h)}</th>`).join('')}
            </tr>
          </thead>
          <tbody>
            ${rows.map(row => `
              <tr>
                ${row.map(cell => {
                  const val = cell === null || cell === undefined ? '' : cell;
                  const isNum = typeof val === 'number';
                  const cls = isNum ? 'num' : 'txt';
                  return `<td class="${cls}">${this.escapeXml(val.toString())}</td>`;
                }).join('')}
              </tr>
            `).join('')}
          </tbody>
        </table>
      </body>
      </html>
    `;

    const blob = new Blob(['\ufeff', html], { type: 'application/vnd.ms-excel;charset=utf-8' });
    this.downloadBlob(blob, cleanFileName);
  }

  /**
   * Exporta a formato CSV estándar con delimitador de coma y BOM UTF-8 para Excel.
   */
  public exportCsv(
    fileName: string,
    headers: string[],
    rows: (string | number | null | undefined)[][]
  ): void {
    const cleanFileName = fileName.endsWith('.csv') ? fileName : `${fileName}.csv`;

    const escapeCsv = (val: string | number | null | undefined) => {
      if (val === null || val === undefined) return '""';
      const str = val.toString().replace(/"/g, '""');
      return `"${str}"`;
    };

    const lines: string[] = [];
    lines.push(headers.map(escapeCsv).join(','));

    for (const row of rows) {
      lines.push(row.map(escapeCsv).join(','));
    }

    const csvContent = lines.join('\r\n');
    const blob = new Blob(['\ufeff', csvContent], { type: 'text/csv;charset=utf-8;' });
    this.downloadBlob(blob, cleanFileName);
  }

  private escapeXml(unsafe: string): string {
    return unsafe
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&apos;');
  }

  private downloadBlob(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.rel = 'noopener';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    setTimeout(() => URL.revokeObjectURL(url), 60000);
  }
}
