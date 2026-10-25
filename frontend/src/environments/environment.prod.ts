export const environment = {
  production: true,
  apiUrl: 'https://lms-primaria.onrender.com/api',
  recaptchaSiteKey: '6LcMQNgtAAAAADA_qovyBzaHt_EDrKEMxTyCeqXo',
  // Client ID de Google Identity Services. Vacío = botón de Google oculto.
  googleClientId: '',
  // Debe coincidir con Privacy:PolicyVersion del backend.
  dataPolicyVersion: '2026-10-19',
  // Página pública de la política (relativa al base-href del sitio).
  dataPolicyUrl: 'privacidad.html',
  // Habilita el botón de restauración de BD en el panel de administración.
  // La API solo ejecuta la restauración si Render tiene las banderas Features__* correspondientes.
  databaseRestoreEnabled: true
};
