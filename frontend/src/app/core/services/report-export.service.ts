import { Injectable } from '@angular/core';
import jsPDF from 'jspdf';
import autoTable from 'jspdf-autotable';

export interface SummaryCardItem {
  label: string;
  value: string | number;
  color?: 'blue' | 'green' | 'yellow' | 'red' | 'purple' | 'slate';
}

export interface PdfReportOptions {
  title: string;
  subtitle?: string;
  author?: string;
  institution?: string;
  summaryCards?: SummaryCardItem[];
  headers: string[];
  rows: (string | number | boolean | null | undefined)[][];
  filename: string;
  orientation?: 'portrait' | 'landscape';
}

@Injectable({
  providedIn: 'root'
})
export class ReportExportService {

  /**
   * Exporta datos tabulares a un archivo CSV con codificación UTF-8 BOM
   * compatible de inmediato con Microsoft Excel, Google Sheets y LibreOffice.
   */
  public exportToCsv(
    filename: string,
    headers: string[],
    rows: (string | number | boolean | null | undefined)[][]
  ): void {
    const sanitizeField = (value: any): string => {
      if (value === null || value === undefined) return '""';
      const str = String(value).replace(/"/g, '""');
      return `"${str}"`;
    };

    const headerLine = headers.map(sanitizeField).join(',');
    const dataLines = rows.map(row => row.map(sanitizeField).join(','));
    const csvContent = '\uFEFF' + [headerLine, ...dataLines].join('\r\n');

    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');

    const cleanFilename = filename.endsWith('.csv') ? filename : `${filename}.csv`;
    link.setAttribute('href', url);
    link.setAttribute('download', cleanFilename);
    link.style.visibility = 'hidden';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  }

  /**
   * Genera y descarga un reporte profesional en PDF con encabezado institucional,
   * resumen métrico en tarjetas, tabla estructurada y pie de página con paginación.
   */
  public exportToPdf(options: PdfReportOptions): void {
    const orientation = options.orientation || (options.headers.length > 5 ? 'landscape' : 'portrait');
    const doc = new jsPDF({
      orientation: orientation,
      unit: 'mm',
      format: 'a4'
    });

    const pageWidth = doc.internal.pageSize.getWidth();
    const pageHeight = doc.internal.pageSize.getHeight();
    const institution = options.institution || 'LMS Primaria • Sistema de Gestión Académica';
    const author = options.author || 'Usuario Autorizado';
    const now = new Date();
    const dateStr = now.toLocaleDateString('es-CO', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });

    // 1. BANNER SUPERIOR INSTITUCIONAL
    const bannerHeight = 28;
    doc.setFillColor(30, 41, 59); // Slate-800
    doc.rect(0, 0, pageWidth, bannerHeight, 'F');

    // Acento decorativo (Línea de color primario)
    doc.setFillColor(56, 189, 248); // Sky blue
    doc.rect(0, bannerHeight - 2, pageWidth, 2, 'F');

    // Título y Nombre Institucional en Banner
    doc.setTextColor(255, 255, 255);
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(14);
    doc.text(institution.toUpperCase(), 14, 12);

    doc.setFont('helvetica', 'normal');
    doc.setFontSize(9);
    doc.setTextColor(203, 213, 225); // Slate-300
    doc.text(`Generado el: ${dateStr}  |  Emitido por: ${author}`, 14, 20);

    let currentY = bannerHeight + 10;

    // 2. TÍTULO DEL REPORTE
    doc.setTextColor(15, 23, 42); // Slate-900
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(16);
    doc.text(options.title, 14, currentY);
    currentY += 6;

    if (options.subtitle) {
      doc.setFont('helvetica', 'normal');
      doc.setFontSize(10);
      doc.setTextColor(100, 116, 139); // Slate-500
      doc.text(options.subtitle, 14, currentY);
      currentY += 6;
    }

    currentY += 2;

    // 3. TARJETAS DE RESUMEN MÉTRICO (Si se proporcionan)
    if (options.summaryCards && options.summaryCards.length > 0) {
      const cardCount = options.summaryCards.length;
      const margin = 14;
      const availableWidth = pageWidth - (margin * 2);
      const gap = 4;
      const cardWidth = (availableWidth - (gap * (cardCount - 1))) / cardCount;
      const cardHeight = 16;

      options.summaryCards.forEach((card, index) => {
        const cardX = margin + (index * (cardWidth + gap));
        
        // Color de borde y fondo suave
        let bgR = 248, bgG = 250, bgB = 252;
        let borderR = 226, borderG = 232, borderB = 240;
        let textR = 15, textG = 23, textB = 42;

        if (card.color === 'green') {
          bgR = 240; bgG = 253; bgB = 244;
          borderR = 134; borderG = 239; borderB = 172;
          textR = 22; textG = 101; textB = 52;
        } else if (card.color === 'yellow') {
          bgR = 254; bgG = 252; bgB = 232;
          borderR = 253; borderG = 224; borderB = 71;
          textR = 133; textG = 77; textB = 14;
        } else if (card.color === 'red') {
          bgR = 254; bgG = 242; bgB = 242;
          borderR = 252; borderG = 165; borderB = 165;
          textR = 153; textG = 27; textB = 27;
        } else if (card.color === 'blue') {
          bgR = 240; bgG = 249; bgB = 255;
          borderR = 186; borderG = 230; borderB = 253;
          textR = 7; textG = 89; textB = 133;
        }

        // Rectángulo con esquinas redondeadas
        doc.setFillColor(bgR, bgG, bgB);
        doc.setDrawColor(borderR, borderG, borderB);
        doc.roundedRect(cardX, currentY, cardWidth, cardHeight, 2, 2, 'FD');

        // Valor numérico / texto destacado
        doc.setTextColor(textR, textG, textB);
        doc.setFont('helvetica', 'bold');
        doc.setFontSize(11);
        doc.text(String(card.value), cardX + 4, currentY + 7);

        // Etiqueta del resumen
        doc.setFont('helvetica', 'normal');
        doc.setFontSize(7.5);
        doc.setTextColor(100, 116, 139);
        const truncatedLabel = card.label.length > 25 ? card.label.substring(0, 23) + '..' : card.label;
        doc.text(truncatedLabel, cardX + 4, currentY + 12.5);
      });

      currentY += cardHeight + 8;
    }

    // 4. TABLA DE DATOS CON JSPDF-AUTOTABLE
    const tableRows = options.rows.map(row => 
      row.map(val => (val === null || val === undefined ? '-' : String(val)))
    );

    const runAutoTable = (d: jsPDF, opts: any) => {
      if (typeof autoTable === 'function') {
        autoTable(d, opts);
      } else if (typeof (autoTable as any)?.default === 'function') {
        (autoTable as any).default(d, opts);
      } else if (typeof (d as any).autoTable === 'function') {
        (d as any).autoTable(opts);
      }
    };

    runAutoTable(doc, {
      startY: currentY,
      head: [options.headers],
      body: tableRows,
      theme: 'grid',
      styles: {
        font: 'helvetica',
        fontSize: 8.5,
        cellPadding: 2.8,
        textColor: [30, 41, 59],
        lineColor: [226, 232, 240],
        lineWidth: 0.2,
        valign: 'middle'
      },
      headStyles: {
        fillColor: [30, 41, 59], // Slate-800
        textColor: [255, 255, 255],
        fontStyle: 'bold',
        fontSize: 9,
        halign: 'left'
      },
      alternateRowStyles: {
        fillColor: [248, 250, 252] // Slate-50
      },
      margin: { left: 14, right: 14, bottom: 20 },
      didDrawPage: (data: any) => {
        // PIE DE PÁGINA EN CADA HOJA
        const totalPages = (doc.internal as any).getNumberOfPages 
          ? (doc.internal as any).getNumberOfPages() 
          : doc.getNumberOfPages();
        const currentPage = data.pageNumber;

        doc.setFont('helvetica', 'normal');
        doc.setFontSize(8);
        doc.setTextColor(148, 163, 184); // Slate-400

        // Línea divisoria de pie de página
        doc.setDrawColor(226, 232, 240);
        doc.line(14, pageHeight - 12, pageWidth - 14, pageHeight - 12);

        doc.text('LMS Primaria - Reporte Oficial de la Plataforma', 14, pageHeight - 7);
        const pageText = `Página ${currentPage} de ${totalPages}`;
        const pageTextWidth = doc.getTextWidth(pageText);
        doc.text(pageText, pageWidth - 14 - pageTextWidth, pageHeight - 7);
      }
    });

    const cleanFilename = options.filename.endsWith('.pdf') ? options.filename : `${options.filename}.pdf`;
    doc.save(cleanFilename);
  }
}
