import { PasswordPolicyService } from './password-policy.service';

describe('PasswordPolicyService', () => {
  let service: PasswordPolicyService;

  beforeEach(() => {
    service = new PasswordPolicyService();
  });

  it('acepta una contraseña de 8 caracteres con todos los requisitos', () => {
    expect(service.validate('Abcdef1@').isValid).toBeTrue();
  });

  it('acepta una contraseña de 64 caracteres', () => {
    expect(service.validate('Aa1@' + 'a'.repeat(60)).isValid).toBeTrue();
  });

  it('rechaza contraseñas de menos de 8 caracteres', () => {
    const result = service.validate('Abc1@a');
    expect(result.isValid).toBeFalse();
    expect(result.errors.join(' ')).toContain('mínimo 8');
    expect(result.strengthLabel).not.toBe('Buena');
    expect(result.strengthScore).toBeLessThanOrEqual(34);
  });

  it('rechaza contraseñas de más de 64 caracteres', () => {
    const result = service.validate('Aa1@' + 'a'.repeat(61));
    expect(result.isValid).toBeFalse();
    expect(result.errors.join(' ')).toContain('64');
    expect(result.strengthLabel).not.toBe('Buena');
  });

  it('rechaza contraseñas que contienen el nombre o el documento', () => {
    expect(service.validate('Sneider9!', 'sneider', 'Sneider Martinez').isValid).toBeFalse();
  });

  it('genera una contraseña temporal de 12 caracteres que cumple la política', () => {
    const generated = service.generateSecurePassword();
    expect(generated.length).toBe(12);
    expect(service.validate(generated).isValid).toBeTrue();
  });
});
