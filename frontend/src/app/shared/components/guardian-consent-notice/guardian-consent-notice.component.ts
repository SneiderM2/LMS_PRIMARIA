import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ConsentPayload } from '../../../core/models/user.model';
import { environment } from '../../../../environments/environment';

/**
 * Componente Standalone de Aviso de Privacidad y Consentimiento Informado para Padres/Acudientes
 * redactado en Lenguaje Claro conforme a la Guía de la Superintendencia de Industria y Comercio (SIC),
 * Ley 1581 de 2012 (Art. 7, 9 y 12), Decreto 1377 de 2013 y Ley 1098 de 2006 (Infancia y Adolescencia).
 *
 * Incluye casillas independientes para finalidades esenciales (obligatorias) y opcionales (voluntarias).
 */
@Component({
  selector: 'app-guardian-consent-notice',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <section class="sic-consent-container" aria-labelledby="sic-notice-heading">
      <header class="sic-consent-header">
        <span class="sic-badge">Protección de Datos de Niños y Niñas · Ley 1581 de 2012 y Ley 1098 de 2006</span>
        <h3 id="sic-notice-heading" class="sic-title">
          Aviso de Privacidad y Consentimiento Informado para Padres, Madres o Acudientes
        </h3>
        <p class="sic-plain-summary">
          <strong>En lenguaje claro (Exigencia SIC):</strong> En nuestra plataforma educativa cuidamos la información
          de tu hijo(a). Solo usamos sus datos escolares para sus clases, misiones y calificaciones. Nunca vendemos
          ni compartimos sus datos con fines comerciales, y puedes consultar, corregir o pedir la eliminación de su
          información cuando quieras (Derechos ARCO: 10 días hábiles para consultas y 15 días hábiles para reclamos).
        </p>
      </header>

      <fieldset class="sic-fieldset">
        <legend class="sic-legend">1. Datos del Padre, Madre o Representante Legal</legend>

        <div class="sic-grid">
          <div class="sic-field">
            <label for="guardianDocType">Tipo de documento</label>
            <select
              id="guardianDocType"
              name="guardianDocumentType"
              [(ngModel)]="guardianDocumentType"
              (ngModelChange)="emitChanges()"
              class="sic-select">
              <option value="CC">Cédula de Ciudadanía (CC)</option>
              <option value="CE">Cédula de Extranjería (CE)</option>
              <option value="TI">Tarjeta de Identidad (TI)</option>
              <option value="PEP">Permiso Especial (PEP)</option>
              <option value="PPT">Permiso por Protección Temporal (PPT)</option>
              <option value="PASAPORTE">Pasaporte</option>
            </select>
          </div>

          <div class="sic-field">
            <label for="guardianDocNum">Número de documento del acudiente</label>
            <input
              id="guardianDocNum"
              type="text"
              name="guardianDocument"
              [(ngModel)]="guardianDocument"
              (ngModelChange)="emitChanges()"
              inputmode="numeric"
              autocorrect="off"
              autocomplete="off"
              maxlength="30"
              placeholder="Ej. 1020304050 (se guarda protegido)"
              class="sic-input" />
          </div>
        </div>

        <div class="sic-field">
          <label for="guardianFullNameInput">Nombre completo del padre, madre o acudiente</label>
          <input
            id="guardianFullNameInput"
            type="text"
            name="guardianFullName"
            [(ngModel)]="guardianFullName"
            (ngModelChange)="emitChanges()"
            autocomplete="name"
            maxlength="150"
            placeholder="Nombres y apellidos completos del representante legal"
            class="sic-input" />
        </div>
      </fieldset>

      <fieldset class="sic-fieldset">
        <legend class="sic-legend">2. Finalidades del Tratamiento (Casillas Separadas)</legend>

        <label class="sic-check-card essential" for="chkEssential">
          <input
            id="chkEssential"
            type="checkbox"
            name="autorizaServicioEducativo"
            [(ngModel)]="autorizaServicioEducativo"
            (ngModelChange)="onEssentialChange()" />
          <div>
            <strong>Finalidad Esencial Educativa (Obligatoria)</strong>
            <p>
              Autorizo como representante legal el tratamiento de los datos académicos de mi hijo(a) únicamente
              para gestionar sus clases, tareas, calificaciones y retroalimentación pedagógica escolar.
            </p>
          </div>
        </label>

        <label class="sic-check-card optional" for="chkOptional">
          <input
            id="chkOptional"
            type="checkbox"
            name="autorizaDatosOpcionales"
            [(ngModel)]="autorizaDatosOpcionales"
            (ngModelChange)="emitChanges()" />
          <div>
            <strong>Finalidades Opcionales y Estadísticas (Voluntaria — No condiciona el servicio)</strong>
            <p>
              Autorizo de manera voluntaria el uso de estadísticas pedagógicas no esenciales, encuestas de mejora
              escolar y reconocimientos en actividades extracurriculares del colegio.
            </p>
          </div>
        </label>

        <label class="sic-check-card cloud" for="chkInternational">
          <input
            id="chkInternational"
            type="checkbox"
            name="autorizaTransmisionInternacional"
            [(ngModel)]="autorizaTransmisionInternacional"
            (ngModelChange)="emitChanges()" />
          <div>
            <strong>Almacenamiento Seguro en la Nube (Art. 26 Ley 1581 de 2012)</strong>
            <p>
              He sido informado(a) de que las tareas y datos escolares se alojan con cifrado TLS en servidores
              en la nube de alta seguridad (Supabase / Render) bajo estándares internacionales de protección.
            </p>
          </div>
        </label>
      </fieldset>
    </section>
  `,
  styles: [`
    .sic-consent-container {
      border: 1px solid rgba(59, 130, 246, 0.35);
      border-radius: 12px;
      padding: 1rem;
      background: rgba(239, 246, 255, 0.65);
      margin: 0.75rem 0;
      text-align: left;
    }
    .sic-badge {
      display: inline-block;
      font-size: 0.72rem;
      font-weight: 700;
      color: #1d4ed8;
      background: #dbeafe;
      padding: 0.2rem 0.55rem;
      border-radius: 999px;
      margin-bottom: 0.4rem;
    }
    .sic-title {
      margin: 0 0 0.4rem 0;
      font-size: 1rem;
      font-weight: 700;
      color: #1e293b;
    }
    .sic-plain-summary {
      font-size: 0.84rem;
      line-height: 1.45;
      color: #334155;
      margin: 0 0 0.75rem 0;
    }
    .sic-fieldset {
      border: 1px solid #cbd5e1;
      border-radius: 8px;
      padding: 0.75rem;
      margin: 0 0 0.75rem 0;
    }
    .sic-legend {
      font-size: 0.82rem;
      font-weight: 700;
      color: #0f172a;
      padding: 0 0.35rem;
    }
    .sic-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
      gap: 0.65rem;
      margin-bottom: 0.65rem;
    }
    .sic-field label {
      display: block;
      font-size: 0.8rem;
      font-weight: 600;
      color: #1e293b;
      margin-bottom: 0.25rem;
    }
    .sic-input, .sic-select {
      width: 100%;
      padding: 0.5rem 0.65rem;
      border-radius: 8px;
      border: 1px solid #94a3b8;
      font-size: 0.88rem;
      box-sizing: border-box;
    }
    .sic-check-card {
      display: flex;
      align-items: flex-start;
      gap: 0.6rem;
      padding: 0.6rem;
      border-radius: 8px;
      background: #ffffff;
      border: 1px solid #e2e8f0;
      margin-bottom: 0.5rem;
      cursor: pointer;
    }
    .sic-check-card input[type="checkbox"] {
      accent-color: #2563eb;
      width: 1.1rem;
      height: 1.1rem;
      margin-top: 0.15rem;
      flex-shrink: 0;
    }
    .sic-check-card strong {
      display: block;
      font-size: 0.84rem;
      color: #0f172a;
    }
    .sic-check-card p {
      margin: 0.2rem 0 0 0;
      font-size: 0.78rem;
      color: #475569;
      line-height: 1.35;
    }
  `]
})
export class GuardianConsentNoticeComponent {
  @Input() policyVersion: string = environment.dataPolicyVersion;
  @Input() guardianFullName: string = '';
  @Input() guardianDocument: string = '';
  @Input() guardianDocumentType: string = 'CC';
  @Input() autorizaServicioEducativo: boolean = false;
  @Input() autorizaDatosOpcionales: boolean = false;
  @Input() autorizaTransmisionInternacional: boolean = true;

  @Output() consentChange = new EventEmitter<ConsentPayload>();

  public onEssentialChange(): void {
    this.emitChanges();
  }

  public emitChanges(): void {
    this.consentChange.emit({
      version: this.policyVersion,
      guardianFullName: this.guardianFullName.trim(),
      guardianDocument: this.guardianDocument.trim(),
      guardianDocumentType: this.guardianDocumentType,
      guardianAuthorization: this.autorizaServicioEducativo,
      autorizaServicioEducativo: this.autorizaServicioEducativo,
      autorizaDatosOpcionales: this.autorizaDatosOpcionales,
      autorizaTransmisionInternacional: this.autorizaTransmisionInternacional
    });
  }
}
