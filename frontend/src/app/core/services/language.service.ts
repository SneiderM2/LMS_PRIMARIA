import { Injectable, signal, computed } from '@angular/core';

export type SupportedLanguage = 'es' | 'en' | 'zh' | 'ja' | 'pt';

export interface LanguageOption {
  code: SupportedLanguage;
  label: string;
  nativeName: string;
  flag: string;
}

export const SUPPORTED_LANGUAGES: LanguageOption[] = [
  { code: 'es', label: 'Español', nativeName: 'Español', flag: '🇪🇸' },
  { code: 'en', label: 'English', nativeName: 'English', flag: '🇺🇸' },
  { code: 'zh', label: 'Chino', nativeName: '中文', flag: '🇨🇳' },
  { code: 'ja', label: 'Japonés', nativeName: '日本語', flag: '🇯🇵' },
  { code: 'pt', label: 'Portugués', nativeName: 'Português', flag: '🇧🇷' }
];

const TRANSLATIONS: Record<SupportedLanguage, Record<string, string>> = {
  es: {
    // General & Navegación
    'app.title': 'Aula Mágica - LMS Primaria',
    'app.subtitle': 'Plataforma Virtual de Primaria',
    'nav.home': 'Inicio',
    'nav.login': 'Iniciar Sesión',
    'nav.logout': 'Cerrar Sesión',
    'nav.adminPanel': 'Panel Directivo',
    'nav.inspector': 'Auditoría & Logs',
    'nav.users': 'Directorio de Usuarios',
    'nav.courses': 'Materias y Cursos',
    'nav.switchLang': 'Idioma',

    // Login & Registro
    'login.title': 'Aula Mágica',
    'login.subtitle': 'Plataforma Virtual de Primaria',
    'login.tabSignIn': '🔑 Iniciar Sesión',
    'login.tabSignUp': '✨ Crear Cuenta',
    'login.idLabel': 'Carnet o Documento de Identidad',
    'login.idPlaceholder': 'Ej: EST-101, DOC-2024 o 1029384756',
    'login.passwordLabel': 'Tu Contraseña',
    'login.btnSubmit': '🚀 ¡Entrar a mi Aula Virtual!',
    'login.btnLoading': '⏳ Abriendo las puertas...',
    'login.orContinueWith': 'O continúa con',
    'login.noAccount': '¿Aún no tienes cuenta escolar?',
    'login.registerHere': 'Regístrate gratis aquí',
    'login.haveAccount': '¿Ya tienes una cuenta registrada?',
    'login.loginHere': 'Inicia sesión aquí',

    // Roles
    'role.student': 'Estudiante',
    'role.teacher': 'Docente',
    'role.admin': 'Administrador',
    'role.superAdmin': 'Super Administrador',

    // Auditoría & Inspector de Sistema
    'audit.title': 'Auditoría Global del Sistema y Registros',
    'audit.subtitle': 'Monitoreo de seguridad, peticiones HTTP y actividades en tiempo real.',
    'audit.search': 'Buscar en logs (IP, usuario, acción, ruta)...',
    'audit.filterSeverity': 'Severidad / Nivel',
    'audit.filterRole': 'Rol de Usuario',
    'audit.filterDate': 'Fecha',
    'audit.filterAll': 'Todos',
    'audit.refresh': 'Recargar',
    'audit.download': 'Descargar Log Completo',
    'audit.clear': 'Limpiar Log',
    'audit.copy': 'Copiar',
    'audit.noLogs': 'No se encontraron eventos registrados con los filtros seleccionados.',
    'audit.levelInfo': 'INFO',
    'audit.levelAudit': 'AUDIT',
    'audit.levelWarning': 'WARNING',
    'audit.levelError': 'ERROR',
    'audit.colTime': 'Fecha y Hora',
    'audit.colLevel': 'Nivel',
    'audit.colMethodStatus': 'Método / Estado',
    'audit.colAction': 'Acción / Ruta',
    'audit.colUser': 'Usuario',
    'audit.colIp': 'IP',
    'audit.colElapsed': 'Tiempo',
    'audit.colDetails': 'Detalles',
    'audit.btnInspect': 'Ver Traza',
    'audit.totalEvents': 'Total de eventos:',
    'audit.page': 'Página',
    'audit.prev': 'Anterior',
    'audit.next': 'Siguiente',

    // Común
    'common.close': 'Cerrar',
    'common.cancel': 'Cancelar',
    'common.confirm': 'Confirmar',
    'common.anonymous': 'Anónimo',
    'common.copied': 'Copiado al portapapeles'
  },
  en: {
    'app.title': 'Magic Classroom - Primary LMS',
    'app.subtitle': 'Primary Virtual Platform',
    'nav.home': 'Home',
    'nav.login': 'Sign In',
    'nav.logout': 'Sign Out',
    'nav.adminPanel': 'Admin Dashboard',
    'nav.inspector': 'Audit & Logs',
    'nav.users': 'User Directory',
    'nav.courses': 'Courses & Subjects',
    'nav.switchLang': 'Language',

    'login.title': 'Magic Classroom',
    'login.subtitle': 'Primary School Virtual Platform',
    'login.tabSignIn': '🔑 Sign In',
    'login.tabSignUp': '✨ Create Account',
    'login.idLabel': 'Student ID or National ID',
    'login.idPlaceholder': 'E.g., EST-101, DOC-2024 or 1029384756',
    'login.passwordLabel': 'Password',
    'login.btnSubmit': '🚀 Enter Magic Classroom!',
    'login.btnLoading': '⏳ Opening doors...',
    'login.orContinueWith': 'Or continue with',
    'login.noAccount': 'Don\'t have an account yet?',
    'login.registerHere': 'Register for free here',
    'login.haveAccount': 'Already have an account?',
    'login.loginHere': 'Sign in here',

    'role.student': 'Student',
    'role.teacher': 'Teacher',
    'role.admin': 'Administrator',
    'role.superAdmin': 'Super Admin',

    'audit.title': 'Global System Audit & Logs',
    'audit.subtitle': 'Real-time security monitoring, HTTP requests, and user activities.',
    'audit.search': 'Search logs (IP, user, action, path)...',
    'audit.filterSeverity': 'Severity / Level',
    'audit.filterRole': 'User Role',
    'audit.filterDate': 'Date',
    'audit.filterAll': 'All',
    'audit.refresh': 'Refresh',
    'audit.download': 'Download Full Log',
    'audit.clear': 'Clear Log',
    'audit.copy': 'Copy',
    'audit.noLogs': 'No event logs found matching current criteria.',
    'audit.levelInfo': 'INFO',
    'audit.levelAudit': 'AUDIT',
    'audit.levelWarning': 'WARNING',
    'audit.levelError': 'ERROR',
    'audit.colTime': 'Date & Time',
    'audit.colLevel': 'Level',
    'audit.colMethodStatus': 'Method / Status',
    'audit.colAction': 'Action / Path',
    'audit.colUser': 'User',
    'audit.colIp': 'IP',
    'audit.colElapsed': 'Elapsed',
    'audit.colDetails': 'Details',
    'audit.btnInspect': 'Inspect Trace',
    'audit.totalEvents': 'Total events:',
    'audit.page': 'Page',
    'audit.prev': 'Previous',
    'audit.next': 'Next',

    'common.close': 'Close',
    'common.cancel': 'Cancel',
    'common.confirm': 'Confirm',
    'common.anonymous': 'Anonymous',
    'common.copied': 'Copied to clipboard'
  },
  zh: {
    'app.title': '魔法教室 - 小学LMS',
    'app.subtitle': '小学在线教育云平台',
    'nav.home': '首页',
    'nav.login': '登录',
    'nav.logout': '退出登录',
    'nav.adminPanel': '管理面板',
    'nav.inspector': '系统审计与日志',
    'nav.users': '用户管理',
    'nav.courses': '课程与科目',
    'nav.switchLang': '语言',

    'login.title': '魔法教室',
    'login.subtitle': '小学在线教育云平台',
    'login.tabSignIn': '🔑 登录账号',
    'login.tabSignUp': '✨ 注册新账号',
    'login.idLabel': '学号或身份证号',
    'login.idPlaceholder': '例如: EST-101, DOC-2024 或 1029384756',
    'login.passwordLabel': '登录密码',
    'login.btnSubmit': '🚀 进入我的魔法教室！',
    'login.btnLoading': '⏳ 正在打开...',
    'login.orContinueWith': '或使用以下方式继续',
    'login.noAccount': '还没有学校账号？',
    'login.registerHere': '点此免费注册',
    'login.haveAccount': '已有注册账号？',
    'login.loginHere': '点此登录',

    'role.student': '学生',
    'role.teacher': '教师',
    'role.admin': '管理员',
    'role.superAdmin': '超级管理员',

    'audit.title': '全局系统审计与日志监控',
    'audit.subtitle': '实时安全监控、HTTP请求追踪及用户行为记录。',
    'audit.search': '搜索日志（IP、用户、操作、路由）...',
    'audit.filterSeverity': '日志级别',
    'audit.filterRole': '用户角色',
    'audit.filterDate': '日期',
    'audit.filterAll': '全部',
    'audit.refresh': '刷新',
    'audit.download': '下载完整日志',
    'audit.clear': '清空日志',
    'audit.copy': '复制',
    'audit.noLogs': '未找到符合条件的日志记录。',
    'audit.levelInfo': '信息',
    'audit.levelAudit': '审计',
    'audit.levelWarning': '警告',
    'audit.levelError': '错误',
    'audit.colTime': '时间戳',
    'audit.colLevel': '级别',
    'audit.colMethodStatus': '方法 / 状态',
    'audit.colAction': '操作 / 路径',
    'audit.colUser': '用户',
    'audit.colIp': 'IP地址',
    'audit.colElapsed': '耗时',
    'audit.colDetails': '详细信息',
    'audit.btnInspect': '查看堆栈',
    'audit.totalEvents': '事件总数:',
    'audit.page': '页码',
    'audit.prev': '上一页',
    'audit.next': '下一页',

    'common.close': '关闭',
    'common.cancel': '取消',
    'common.confirm': '确认',
    'common.anonymous': '匿名',
    'common.copied': '已复制到剪贴板'
  },
  ja: {
    'app.title': '魔法の教室 - 小学校LMS',
    'app.subtitle': '小学校向けバーチャル学習プラットフォーム',
    'nav.home': 'ホーム',
    'nav.login': 'ログイン',
    'nav.logout': 'ログアウト',
    'nav.adminPanel': '管理者パネル',
    'nav.inspector': '監査とログ',
    'nav.users': 'ユーザー名簿',
    'nav.courses': '教科・コース',
    'nav.switchLang': '言語',

    'login.title': '魔法の教室',
    'login.subtitle': '小学校向けバーチャル学習プラットフォーム',
    'login.tabSignIn': '🔑 ログイン',
    'login.tabSignUp': '✨ 新規登録',
    'login.idLabel': '学籍番号または身分証明書番号',
    'login.idPlaceholder': '例: EST-101, DOC-2024 または 1029384756',
    'login.passwordLabel': 'パスワード',
    'login.btnSubmit': '🚀 教室へ入る！',
    'login.btnLoading': '⏳ 読み込み中...',
    'login.orContinueWith': 'または以下で続行',
    'login.noAccount': 'アカウントをお持ちでないですか？',
    'login.registerHere': '無料で新規登録',
    'login.haveAccount': 'すでにアカウントをお持ちですか？',
    'login.loginHere': 'ログインはこちら',

    'role.student': '児童・生徒',
    'role.teacher': '教員',
    'role.admin': '管理者',
    'role.superAdmin': '特権管理者',

    'audit.title': 'グローバルシステム監査ログ',
    'audit.subtitle': 'リアルタイムのセキュリティ監視、HTTP通信、ユーザー活動記録。',
    'audit.search': 'ログを検索 (IP、ユーザー、操作、パス)...',
    'audit.filterSeverity': '重要度 / レベル',
    'audit.filterRole': 'ユーザー権限',
    'audit.filterDate': '日付',
    'audit.filterAll': 'すべて',
    'audit.refresh': '更新',
    'audit.download': '完全ログをダウンロード',
    'audit.clear': 'ログ消去',
    'audit.copy': 'コピー',
    'audit.noLogs': '該当するログが見つかりませんでした。',
    'audit.levelInfo': '情報',
    'audit.levelAudit': '監査',
    'audit.levelWarning': '警告',
    'audit.levelError': 'エラー',
    'audit.colTime': '日時',
    'audit.colLevel': 'レベル',
    'audit.colMethodStatus': 'メソッド / ステータス',
    'audit.colAction': '操作 / パス',
    'audit.colUser': 'ユーザー',
    'audit.colIp': 'IP',
    'audit.colElapsed': '所要時間',
    'audit.colDetails': '詳細',
    'audit.btnInspect': 'トレース確認',
    'audit.totalEvents': '合計イベント数:',
    'audit.page': 'ページ',
    'audit.prev': '前へ',
    'audit.next': '次へ',

    'common.close': '閉じる',
    'common.cancel': 'キャンセル',
    'common.confirm': '確認',
    'common.anonymous': '匿名',
    'common.copied': 'クリップボードにコピーしました'
  },
  pt: {
    'app.title': 'Sala Mágica - LMS Primária',
    'app.subtitle': 'Plataforma Virtual do Ensino Fundamental',
    'nav.home': 'Início',
    'nav.login': 'Entrar',
    'nav.logout': 'Sair',
    'nav.adminPanel': 'Painel Diretivo',
    'nav.inspector': 'Auditoria & Logs',
    'nav.users': 'Diretório de Usuários',
    'nav.courses': 'Matérias e Cursos',
    'nav.switchLang': 'Idioma',

    'login.title': 'Sala Mágica',
    'login.subtitle': 'Plataforma Virtual do Ensino Fundamental',
    'login.tabSignIn': '🔑 Entrar',
    'login.tabSignUp': '✨ Criar Conta',
    'login.idLabel': 'Matrícula ou Documento de Identidade',
    'login.idPlaceholder': 'Ex: EST-101, DOC-2024 ou 1029384756',
    'login.passwordLabel': 'Sua Senha',
    'login.btnSubmit': '🚀 Entrar na Minha Sala Virtual!',
    'login.btnLoading': '⏳ Abrindo as portas...',
    'login.orContinueWith': 'Ou continue com',
    'login.noAccount': 'Ainda não tem conta escolar?',
    'login.registerHere': 'Cadastre-se grátis aqui',
    'login.haveAccount': 'Já possui uma conta?',
    'login.loginHere': 'Faça login aqui',

    'role.student': 'Estudante',
    'role.teacher': 'Professor(a)',
    'role.admin': 'Administrador',
    'role.superAdmin': 'Super Administrador',

    'audit.title': 'Auditoria Global do Sistema e Logs',
    'audit.subtitle': 'Monitoramento de segurança, requisições HTTP e atividades em tempo real.',
    'audit.search': 'Pesquisar logs (IP, usuário, ação, rota)...',
    'audit.filterSeverity': 'Severidade / Nível',
    'audit.filterRole': 'Função do Usuário',
    'audit.filterDate': 'Data',
    'audit.filterAll': 'Todos',
    'audit.refresh': 'Atualizar',
    'audit.download': 'Baixar Log Completo',
    'audit.clear': 'Limpar Log',
    'audit.copy': 'Copiar',
    'audit.noLogs': 'Nenhum log encontrado para os critérios selecionados.',
    'audit.levelInfo': 'INFO',
    'audit.levelAudit': 'AUDIT',
    'audit.levelWarning': 'WARNING',
    'audit.levelError': 'ERROR',
    'audit.colTime': 'Data e Hora',
    'audit.colLevel': 'Nível',
    'audit.colMethodStatus': 'Método / Status',
    'audit.colAction': 'Ação / Rota',
    'audit.colUser': 'Usuário',
    'audit.colIp': 'IP',
    'audit.colElapsed': 'Tempo',
    'audit.colDetails': 'Detalhes',
    'audit.btnInspect': 'Ver Rastreamento',
    'audit.totalEvents': 'Total de eventos:',
    'audit.page': 'Página',
    'audit.prev': 'Anterior',
    'audit.next': 'Próximo',

    'common.close': 'Fechar',
    'common.cancel': 'Cancelar',
    'common.confirm': 'Confirmar',
    'common.anonymous': 'Anônimo',
    'common.copied': 'Copiado para a área de transferência'
  }
};

@Injectable({
  providedIn: 'root'
})
export class LanguageService {
  private readonly STORAGE_KEY = 'lms_selected_language';

  readonly languages = SUPPORTED_LANGUAGES;

  // Signal reactivo para el idioma actual
  currentLang = signal<SupportedLanguage>(this.getInitialLanguage());

  // Signal computado para la opción activa
  currentOption = computed(() => {
    const code = this.currentLang();
    return this.languages.find(l => l.code === code) ?? this.languages[0];
  });

  constructor() {
    // Sincronizar atributo lang del documento HTML
    if (typeof document !== 'undefined') {
      document.documentElement.lang = this.currentLang();
    }
  }

  private getInitialLanguage(): SupportedLanguage {
    if (typeof localStorage !== 'undefined') {
      const saved = localStorage.getItem(this.STORAGE_KEY) as SupportedLanguage;
      if (saved && TRANSLATIONS[saved]) {
        return saved;
      }
    }

    if (typeof navigator !== 'undefined' && navigator.language) {
      const browserPrefix = navigator.language.slice(0, 2).toLowerCase() as SupportedLanguage;
      if (TRANSLATIONS[browserPrefix]) {
        return browserPrefix;
      }
    }

    return 'es';
  }

  public setLanguage(lang: SupportedLanguage): void {
    if (TRANSLATIONS[lang]) {
      this.currentLang.set(lang);
      if (typeof localStorage !== 'undefined') {
        localStorage.setItem(this.STORAGE_KEY, lang);
      }
      if (typeof document !== 'undefined') {
        document.documentElement.lang = lang;
      }
    }
  }

  public translate(key: string, fallback?: string): string {
    const lang = this.currentLang();
    return TRANSLATIONS[lang]?.[key] ?? fallback ?? key;
  }

  public t(key: string, fallback?: string): string {
    return this.translate(key, fallback);
  }
}
