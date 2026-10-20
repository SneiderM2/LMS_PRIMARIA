export const environment = {
  production: false,
  apiUrl: 'http://localhost:5000/api',
  // Clave oficial de prueba de Google reCAPTCHA v2 (admite localhost y 127.0.0.1 sin errores de dominio)
  recaptchaSiteKey: '6LeIxAcTAAAAAJcZVRqyHh71UMIEGNQ_MXjiZKhI',
  // Client ID de Google Identity Services. Vacío = botón de Google oculto.
  googleClientId: '',
  // Debe coincidir con Privacy:PolicyVersion del backend.
  dataPolicyVersion: '2026-10-19',
  // Página pública de la política (relativa al base-href del sitio).
  dataPolicyUrl: 'privacidad.html'
};
