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

// Diccionario de frases exactas de la interfaz
const PHRASES: Record<string, Record<SupportedLanguage, string>> = {
  // Navegación & Encabezados
  'LMS Primaria': { es: 'LMS Primaria', en: 'Primary LMS', zh: '小学 LMS', ja: '小学校 LMS', pt: 'LMS Primário' },
  'Panel Directivo Institucional': { es: 'Panel Directivo Institucional', en: 'Institutional Management Panel', zh: '机构管理面板', ja: '機関管理パネル', pt: 'Painel Diretivo Institucional' },
  'Portal Docente': { es: 'Portal Docente', en: 'Teacher Portal', zh: '教师门户', ja: '教員ポータル', pt: 'Portal do Professor' },
  'Campus Escolar Interactivo': { es: 'Campus Escolar Interactivo', en: 'Interactive School Campus', zh: '互动校园', ja: 'インタラクティブキャンパス', pt: 'Campus Escolar Interativo' },
  'Mi Escuela Mágica': { es: 'Mi Escuela Mágica', en: 'My Magic School', zh: '我的魔法学校', ja: '私の魔法の学校', pt: 'Minha Escola Mágica' },
  'Aula Mágica': { es: 'Aula Mágica', en: 'Magic Classroom', zh: '魔法教室', ja: '魔法の教室', pt: 'Sala Mágica' },
  'Auditoría & Logs': { es: 'Auditoría & Logs', en: 'Audit & Logs', zh: '审计与日志', ja: '監査＆ログ', pt: 'Auditoria & Logs' },
  'Directorio de Usuarios': { es: 'Directorio de Usuarios', en: 'User Directory', zh: '用户目录', ja: 'ユーザー名簿', pt: 'Diretório de Usuários' },
  'Materias y Cursos': { es: 'Materias y Cursos', en: 'Subjects & Courses', zh: '科目与课程', ja: '科目とコース', pt: 'Disciplinas e Cursos' },
  'Salir': { es: 'Salir', en: 'Sign Out', zh: '退出', ja: 'ログアウト', pt: 'Sair' },
  '🚪 Salir': { es: '🚪 Salir', en: '🚪 Sign Out', zh: '🚪 退出', ja: '🚪 ログアウト', pt: '🚪 Sair' },
  'Cerrar sesión': { es: 'Cerrar sesión', en: 'Sign Out', zh: '退出登录', ja: 'ログアウト', pt: 'Encerrar sessão' },
  'Iniciar Sesión': { es: 'Iniciar Sesión', en: 'Sign In', zh: '登录', ja: 'ログイン', pt: 'Entrar' },
  'Crear Cuenta': { es: 'Crear Cuenta', en: 'Sign Up', zh: '创建账号', ja: 'アカウント作成', pt: 'Criar Conta' },

  // Botones y Acciones Comunes
  'Buscar': { es: 'Buscar', en: 'Search', zh: '搜索', ja: '検索', pt: 'Buscar' },
  '🔍 Buscar': { es: '🔍 Buscar', en: '🔍 Search', zh: '🔍 搜索', ja: '🔍 検索', pt: '🔍 Buscar' },
  'Guardar': { es: 'Guardar', en: 'Save', zh: '保存', ja: '保存', pt: 'Salvar' },
  'Cancelar': { es: 'Cancelar', en: 'Cancel', zh: '取消', ja: 'キャンセル', pt: 'Cancelar' },
  '❌ Cancelar': { es: '❌ Cancelar', en: '❌ Cancel', zh: '❌ 取消', ja: '❌ キャンセル', pt: '❌ Cancelar' },
  'Confirmar': { es: 'Confirmar', en: 'Confirm', zh: '确认', ja: '確認', pt: 'Confirmar' },
  'Cerrar': { es: 'Cerrar', en: 'Close', zh: '关闭', ja: '閉じる', pt: 'Fechar' },
  'Editar': { es: 'Editar', en: 'Edit', zh: '编辑', ja: '編集', pt: 'Editar' },
  '✏️ Editar': { es: '✏️ Editar', en: '✏️ Edit', zh: '✏️ 编辑', ja: '✏️ 編集', pt: '✏️ Editar' },
  'Suspender': { es: 'Suspender', en: 'Suspend', zh: '停用', ja: '停止', pt: 'Suspender' },
  '🚫 Suspender': { es: '🚫 Suspender', en: '🚫 Suspend', zh: '🚫 停用', ja: '🚫 停止', pt: '🚫 Suspender' },
  'Habilitar': { es: 'Habilitar', en: 'Enable', zh: '启用', ja: '有効化', pt: 'Habilitar' },
  '✅ Habilitar': { es: '✅ Habilitar', en: '✅ Enable', zh: '✅ 启用', ja: '✅ 有効化', pt: '✅ Habilitar' },
  '➕ Registrar Usuario': { es: '➕ Registrar Usuario', en: '➕ Register User', zh: '➕ 注册用户', ja: '➕ ユーザー登録', pt: '➕ Cadastrar Usuário' },
  '➕ Nueva Materia': { es: '➕ Nueva Materia', en: '➕ New Subject', zh: '➕ 新建科目', ja: '➕ 新しい科目', pt: '➕ Nova Disciplina' },
  '➕ Publicar Nueva Actividad': { es: '➕ Publicar Nueva Actividad', en: '➕ Publish New Activity', zh: '➕ 发布新课题', ja: '➕ 新しい課題を公開', pt: '➕ Publicar Nova Atividade' },
  '➕ Nueva Actividad': { es: '➕ Nueva Actividad', en: '➕ New Activity', zh: '➕ 新建活动', ja: '➕ 新しい課題', pt: '➕ Nova Atividade' },
  '📊 Reporte .CSV': { es: '📊 Reporte .CSV', en: '📊 .CSV Report', zh: '📊 .CSV 报告', ja: '📊 .CSV レポート', pt: '📊 Relatório .CSV' },
  '📄 Reporte .PDF': { es: '📄 Reporte .PDF', en: '📄 .PDF Report', zh: '📄 .PDF 报告', ja: '📄 .PDF レポート', pt: '📄 Relatório .PDF' },
  '💾 Descargar Backup BD': { es: '💾 Descargar Backup BD', en: '💾 Download DB Backup', zh: '💾 下载数据库备份', ja: '💾 DBバックアップDL', pt: '💾 Baixar Backup BD' },
  '📥 Restaurar Backup BD': { es: '📥 Restaurar Backup BD', en: '📥 Restore DB Backup', zh: '📥 恢复数据库备份', ja: '📥 DBバックアップ復元', pt: '📥 Restaurar Backup BD' },
  '🚦 Abrir Semáforo': { es: '🚦 Abrir Semáforo', en: '🚦 Open Traffic Light', zh: '🚦 打开信号灯', ja: '🚦 シグナルを開く', pt: '🚦 Abrir Semáforo' },

  // Estados y Badges
  'Todos': { es: 'Todos', en: 'All', zh: '全部', ja: 'すべて', pt: 'Todos' },
  'Todas': { es: 'Todas', en: 'All', zh: '全部', ja: 'すべて', pt: 'Todas' },
  'Activos': { es: 'Activos', en: 'Active', zh: '活跃', ja: 'アクティブ', pt: 'Ativos' },
  'Suspendidos': { es: 'Suspendidos', en: 'Suspended', zh: '已停用', ja: '停止中', pt: 'Suspensos' },
  '🟢 Activo': { es: '🟢 Activo', en: '🟢 Active', zh: '🟢 活跃', ja: '🟢 アクティブ', pt: '🟢 Ativo' },
  '🔴 Suspendido': { es: '🔴 Suspendido', en: '🔴 Suspended', zh: '🔴 已停用', ja: '🔴 停止中', pt: '🔴 Suspenso' },
  '🟢 Activos': { es: '🟢 Activos', en: '🟢 Active', zh: '🟢 活跃', ja: '🟢 アクティブ', pt: '🟢 Ativos' },
  '🔴 Suspendidos': { es: '🔴 Suspendidos', en: '🔴 Suspended', zh: '🔴 已停用', ja: '🔴 停止中', pt: '🔴 Suspensos' },
  '🟢 Habilitada': { es: '🟢 Habilitada', en: '🟢 Enabled', zh: '🟢 已启用', ja: '🟢 有効', pt: '🟢 Habilitada' },
  '📦 Suspendida': { es: '📦 Suspendida', en: '📦 Suspended', zh: '📦 已停用', ja: '📦 停止中', pt: '📦 Suspensas' },
  '🟢 Publicadas': { es: '🟢 Publicadas', en: '🟢 Published', zh: '🟢 已发布', ja: '🟢 公開中', pt: '🟢 Publicadas' },
  '📦 Archivadas': { es: '📦 Archivadas', en: '📦 Archived', zh: '📦 已归档', ja: '📦 アーカイブ済', pt: '📦 Arquivadas' },
  'En Línea': { es: 'En Línea', en: 'Online', zh: '在线', ja: 'オンライン', pt: 'Online' },
  'Estable': { es: 'Estable', en: 'Stable', zh: '稳定', ja: '安定', pt: 'Estável' },
  'Alerta': { es: 'Alerta', en: 'Alert', zh: '警报', ja: '警告', pt: 'Alerta' },
  'Total': { es: 'Total', en: 'Total', zh: '总计', ja: '合計', pt: 'Total' },
  'Revisión': { es: 'Revisión', en: 'Review', zh: '审核', ja: '確認', pt: 'Revisão' },
  'En Curso': { es: 'En Curso', en: 'In Progress', zh: '进行中', ja: '進行中', pt: 'Em Andamento' },

  // Roles
  'Estudiante': { es: 'Estudiante', en: 'Student', zh: '学生', ja: '児童・生徒', pt: 'Estudante' },
  'Docente': { es: 'Docente', en: 'Teacher', zh: '教师', ja: '教員', pt: 'Docente' },
  'Administrador': { es: 'Administrador', en: 'Administrator', zh: '管理员', ja: '管理者', pt: 'Administrador' },
  'Super Administrador': { es: 'Super Administrador', en: 'Super Admin', zh: '超级管理员', ja: '特権管理者', pt: 'Super Administrador' },
  '🛡️ Super Administrador': { es: '🛡️ Super Administrador', en: '🛡️ Super Admin', zh: '🛡️ 超级管理员', ja: '🛡️ 特権管理者', pt: '🛡️ Super Administrador' },
  '🎒 Alumnos': { es: '🎒 Alumnos', en: '🎒 Students', zh: '🎒 学生', ja: '🎒 生徒', pt: '🎒 Alunos' },
  '🎒 Alumno': { es: '🎒 Alumno', en: '🎒 Student', zh: '🎒 学生', ja: '🎒 生徒', pt: '🎒 Aluno' },
  '👨‍🏫 Docentes': { es: '👨‍🏫 Docentes', en: '👨‍🏫 Teachers', zh: '👨‍🏫 教师', ja: '👨‍🏫 教員', pt: '👨‍🏫 Docentes' },
  '👨‍🏫 Docente': { es: '👨‍🏫 Docente', en: '👨‍🏫 Teacher', zh: '👨‍🏫 教师', ja: '👨‍🏫 教員', pt: '👨‍🏫 Docente' },
  'Docente Titular': { es: 'Docente Titular', en: 'Head Teacher', zh: '主任教师', ja: '担任教師', pt: 'Professor Titular' },

  // Métricas y KPIs
  'Usuarios en Plataforma': { es: 'Usuarios en Plataforma', en: 'Platform Users', zh: '平台用户', ja: '登録ユーザー数', pt: 'Usuários na Plataforma' },
  'Servicios Principales': { es: 'Servicios Principales', en: 'Core Services', zh: '核心服务', ja: '主要サービス', pt: 'Serviços Principais' },
  'Latencia: Óptima': { es: 'Latencia: Óptima', en: 'Latency: Optimal', zh: '延迟：最佳', ja: '遅延：最適', pt: 'Latência: Ótima' },
  'Incidencias (Últimas 24h)': { es: 'Incidencias (Últimas 24h)', en: 'Incidents (Last 24h)', zh: '事件 (近24小时)', ja: 'インシデント (24時間)', pt: 'Incidentes (Últimas 24h)' },
  'Sin fallos críticos registrados': { es: 'Sin fallos críticos registrados', en: 'No critical failures', zh: '无严重故障记录', ja: '重大な障害なし', pt: 'Sem falhas críticas' },
  'Materias Habilitadas': { es: 'Materias Habilitadas', en: 'Enabled Subjects', zh: '已启用科目', ja: '開講科目数', pt: 'Disciplinas Habilitadas' },
  'Matrículas activas': { es: 'Matrículas activas', en: 'Active enrollments', zh: '有效注册', ja: '有効な受講生', pt: 'Matrículas ativas' },
  'Contenido didáctico': { es: 'Contenido didáctico', en: 'Learning content', zh: '教学内容', ja: '教材コンテンツ', pt: 'Conteúdo didático' },
  'Alumnos a Cargo': { es: 'Alumnos a Cargo', en: 'Assigned Students', zh: '负责学生', ja: '担当生徒数', pt: 'Alunos Designados' },
  'En tus materias asignadas': { es: 'En tus materias asignadas', en: 'In your assigned subjects', zh: '在你负责的科目中', ja: '担当科目内', pt: 'Nas disciplinas atribuídas' },
  'Por Calificar': { es: 'Por Calificar', en: 'To Grade', zh: '待评分', ja: '採点待ち', pt: 'Para Avaliar' },
  '¡Estás al día! 🎉': { es: '¡Estás al día! 🎉', en: "You're all caught up! 🎉", zh: '全部批改完成！🎉', ja: 'すべて完了！🎉', pt: 'Tudo em dia! 🎉' },
  'Entregas pendientes': { es: 'Entregas pendientes', en: 'Pending submissions', zh: '待处理提交', ja: '未採点の提出物', pt: 'Entregas pendentes' },
  'Tareas Publicadas': { es: 'Tareas Publicadas', en: 'Published Tasks', zh: '已发布课题', ja: '公開中の課題', pt: 'Tarefas Publicadas' },
  'Disponibles para entrega': { es: 'Disponibles para entrega', en: 'Available for submission', zh: '开放提交中', ja: '提出受付中', pt: 'Disponíveis para entrega' },
  'Tasa de Cumplimiento': { es: 'Tasa de Cumplimiento', en: 'Submission Rate', zh: '完成率', ja: '提出率', pt: 'Taxa de Cumprimento' },
  'Clases Inscritas': { es: 'Clases Inscritas', en: 'Enrolled Classes', zh: '已选课程', ja: '受講クラス', pt: 'Aulas Inscritas' },
  'Materias de tu grado': { es: 'Materias de tu grado', en: 'Subjects of your grade', zh: '你的年级科目', ja: 'あなたの学年の科目', pt: 'Disciplinas do seu ano' },
  'Promedio de Notas': { es: 'Promedio de Notas', en: 'Grade Average', zh: '平均成绩', ja: '平均評定', pt: 'Média de Notas' },
  '¡Excelente rendimiento! 🌟': { es: '¡Excelente rendimiento! 🌟', en: 'Excellent performance! 🌟', zh: '表现优异！🌟', ja: '素晴らしい成績！🌟', pt: 'Excelente desempenho! 🌟' },
  'Tareas Pendientes': { es: 'Tareas Pendientes', en: 'Pending Tasks', zh: '待办任务', ja: '未提出の課題', pt: 'Tarefas Pendentes' },
  '¡Aún estás a tiempo!': { es: '¡Aún estás a tiempo!', en: 'You still have time!', zh: '还有时间赶上！', ja: 'まだ間に合います！', pt: 'Ainda dá tempo!' },
  'Progreso de Tareas': { es: 'Progreso de Tareas', en: 'Task Progress', zh: '任务进度', ja: '課題の進捗', pt: 'Progresso de Tarefas' },

  // Secciones y Tablas
  'Directorio de Cuentas y Accesos': { es: 'Directorio de Cuentas y Accesos', en: 'Account & Access Directory', zh: '账号与访问目录', ja: 'アカウント名簿とアクセス', pt: 'Diretório de Contas e Acessos' },
  'Alta, edición y suspensión lógica con preservación total de notas y entregas.': { es: 'Alta, edición y suspensión lógica con preservación total de notas y entregas.', en: 'Registration, editing, and soft-delete preserving all grades and submissions.', zh: '添加、编辑与软删除，完整保留成绩与提交记录。', ja: '成績や提出物を保持した登録、編集、論理削除。', pt: 'Cadastro, edição e suspensão lógica preservando todas as notas e entregas.' },
  'Buscar por nombre, carnet...': { es: 'Buscar por nombre, carnet...', en: 'Search by name, ID...', zh: '按姓名、学号搜索...', ja: '氏名、IDで検索...', pt: 'Buscar por nome, matrícula...' },
  'Consultando base de datos escolar...': { es: 'Consultando base de datos escolar...', en: 'Querying school database...', zh: '正在查询学校数据库...', ja: 'データベース照会中...', pt: 'Consultando banco de datos...' },
  'Usuario': { es: 'Usuario', en: 'User', zh: '用户', ja: 'ユーザー', pt: 'Usuário' },
  'Carnet / Documento': { es: 'Carnet / Documento', en: 'ID / Document', zh: '学号 / 证件', ja: 'ID / 身分証', pt: 'Matrícula / Documento' },
  'Grado Escolar': { es: 'Grado Escolar', en: 'School Grade', zh: '学年', ja: '学年', pt: 'Ano Escolar' },
  'Fecha de Alta': { es: 'Fecha de Alta', en: 'Registration Date', zh: '注册日期', ja: '登録日', pt: 'Data de Cadastro' },
  'Acciones': { es: 'Acciones', en: 'Actions', zh: '操作', ja: '操作', pt: 'Ações' },
  'Sin resultados': { es: 'Sin resultados', en: 'No results', zh: '无结果', ja: '結果なし', pt: 'Sem resultados' },
  'No se encontraron cuentas con los filtros seleccionados.': { es: 'No se encontraron cuentas con los filtros seleccionados.', en: 'No accounts found with the selected filters.', zh: '未找到符合筛选条件的账号。', ja: '選択した条件に一致するアカウントはありません。', pt: 'Nenhuma conta encontrada com os filtros selecionados.' },

  'Módulo Institucional de Asignaturas': { es: 'Módulo Institucional de Asignaturas', en: 'Institutional Subjects Module', zh: '科目机构模块', ja: '科目管理モジュール', pt: 'Módulo Institucional de Disciplinas' },
  'Gestión de Materias y Docentes Responsables 📚': { es: 'Gestión de Materias y Docentes Responsables 📚', en: 'Management of Subjects & Lead Teachers 📚', zh: '科目与负责教师管理 📚', ja: '科目および担当教員の管理 📚', pt: 'Gestão de Disciplinas e Docentes Responsáveis 📚' },
  'Crea cursos para cada grado (1° a 6° de primaria), asigna al profesor titular y supervisa el aforo de estudiantes.': { es: 'Crea cursos para cada grado (1° a 6° de primaria), asigna al profesor titular y supervisa el aforo de estudiantes.', en: 'Create courses for each grade (1st to 6th), assign the lead teacher and monitor student capacity.', zh: '为每个年级创建课程，分配主任教师并监督学生人数。', ja: '各学年のコースを作成し、担任教師を割り当て、生徒数を管理します。', pt: 'Crie cursos para cada ano, atribua o professor titular e supervisione a capacidade de alunos.' },
  'Materias Registradas en la Institución': { es: 'Materias Registradas en la Institución', en: 'Subjects Registered in the Institution', zh: '机构已注册科目', ja: '登録済みの科目一覧', pt: 'Disciplinas Registradas na Instituição' },
  'Edita asignaciones de docentes y controla la disponibilidad en el portal.': { es: 'Edita asignaciones de docentes y controla la disponibilidad en el portal.', en: 'Edit teacher assignments and control availability on the portal.', zh: '编辑教师分配并控制门户中的可用性。', ja: '教員の割り当てを編集しポータルでの公開状態を管理します。', pt: 'Edite atribuições de professores e controle a disponibilidade no portal.' },
  'Todos los Grados': { es: 'Todos los Grados', en: 'All Grades', zh: '全部年级', ja: 'すべての学年', pt: 'Todos os Anos' },
  'Buscar materia o docente...': { es: 'Buscar materia o docente...', en: 'Search subject or teacher...', zh: '搜索科目或教师...', ja: '科目や教員を検索...', pt: 'Buscar disciplina ou professor...' },
  'Cargando materias institucionales...': { es: 'Cargando materias institucionales...', en: 'Loading institutional subjects...', zh: '正在加载科目...', ja: '科目を読み込み中...', pt: 'Carregando disciplinas...' },
  'Materia': { es: 'Materia', en: 'Subject', zh: '科目', ja: '科目', pt: 'Disciplina' },
  'Grado / Grupo': { es: 'Grado / Grupo', en: 'Grade / Group', zh: '年级 / 班级', ja: '学年 / クラス', pt: 'Ano / Turma' },
  'Docente Asignado': { es: 'Docente Asignado', en: 'Assigned Teacher', zh: '指派教师', ja: '担当教員', pt: 'Professor Designado' },
  'Estudiantes Matriculados': { es: 'Estudiantes Matriculados', en: 'Enrolled Students', zh: '已注册学生', ja: '受講生徒数', pt: 'Estudantes Matriculados' },
  'Sin materias encontradas': { es: 'Sin materias encontradas', en: 'No subjects found', zh: '未找到科目', ja: '科目が見つかりません', pt: 'Nenhuma disciplina encontrada' },
  'No se encontraron cursos que coincidan con la búsqueda.': { es: 'No se encontraron cursos que coincidan con la búsqueda.', en: 'No courses found matching the search.', zh: '未找到符合搜索条件的课程。', ja: '検索条件に一致するコースは見つかりませんでした。', pt: 'Nenhum curso encontrado que corresponda à busca.' },

  'Gestión Académica Activa': { es: 'Gestión Académica Activa', en: 'Active Academic Management', zh: '活跃教务管理', ja: '教務管理稼働中', pt: 'Gestão Acadêmica Ativa' },
  'Gestión de Actividades y Evaluaciones 📚': { es: 'Gestión de Actividades y Evaluaciones 📚', en: 'Activities & Assessments Management 📚', zh: '课题与评估管理 📚', ja: '課題と評価の管理 📚', pt: 'Gestão de Atividades e Avaliações 📚' },
  'Publica tareas interactivas, consulta las entregas de tus alumnos, califica y envía retroalimentación motivacional.': { es: 'Publica tareas interactivas, consulta las entregas de tus alumnos, califica y envía retroalimentación motivacional.', en: 'Publish interactive tasks, check student submissions, grade, and send encouraging feedback.', zh: '发布互动任务，查看学生提交，打分并提供激励性反馈。', ja: '課題を公開し、提出物を確認・採点してフィードバックを送りましょう。', pt: 'Publique tarefas interativas, consulte as entregas dos alunos, avalie e envie feedback.' },
  'Catálogo de Trabajos y Evaluaciones': { es: 'Catálogo de Trabajos y Evaluaciones', en: 'Assignments & Assessments Catalog', zh: '作业与评估目录', ja: '課題・評価一覧', pt: 'Catálogo de Trabalhos e Avaliações' },
  'Supervisa el estado de cada actividad, revisa entregas de estudiantes y archiva evaluaciones.': { es: 'Supervisa el estado de cada actividad, revisa entregas de estudiantes y archiva evaluaciones.', en: 'Supervise activity statuses, review student submissions, and archive assessments.', zh: '监督活动状态，审核学生提交并归档评估。', ja: '各課題の状況を把握し、提出物を採点してアーカイブします。', pt: 'Supervisione o status de cada atividade, revise entregas e arquive avaliações.' },
  'Buscar por título...': { es: 'Buscar por título...', en: 'Search by title...', zh: '按标题搜索...', ja: 'タイトルで検索...', pt: 'Buscar por título...' },
  'Cargando actividades y entregas...': { es: 'Cargando actividades y entregas...', en: 'Loading activities and submissions...', zh: '正在加载活动与提交...', ja: '課題と提出物を読み込み中...', pt: 'Carregando atividades e entregas...' },
  'No hay actividades registradas': { es: 'No hay actividades registradas', en: 'No activities recorded', zh: '暂无活动记录', ja: '登録された課題はありません', pt: 'Nenhuma atividade registrada' },
  'Crea tu primera tarea o recurso educativo usando el botón superior.': { es: 'Crea tu primera tarea o recurso educativo usando el botón superior.', en: 'Create your first task or learning resource using the button above.', zh: '使用上方按钮创建你的第一个任务或教材。', ja: '上のボタンから最初の課題や教材を作成してください。', pt: 'Crie sua primeira tarefa ou recurso usando o botão acima.' },
  'Título de la Actividad': { es: 'Título de la Actividad', en: 'Activity Title', zh: '课题标题', ja: '課題タイトル', pt: 'Título da Atividade' },
  'Materia / Grado': { es: 'Materia / Grado', en: 'Subject / Grade', zh: '科目 / 年级', ja: '科目 / 学年', pt: 'Disciplina / Ano' },
  'Tipo': { es: 'Tipo', en: 'Type', zh: '类型', ja: '種別', pt: 'Tipo' },

  // Estudiante
  'Filtrar por Materia': { es: 'Filtrar por Materia', en: 'Filter by Subject', zh: '按科目筛选', ja: '科目で絞り込み', pt: 'Filtrar por Disciplina' },
  'Elige una materia para ver tus trabajos': { es: 'Elige una materia para ver tus trabajos', en: 'Choose a subject to view your assignments', zh: '选择一个科目查看你的作业', ja: '科目を選択して課題を確認しましょう', pt: 'Escolha uma disciplina para ver seus trabalhos' },
  'Trabajos y Recursos Asignados': { es: 'Trabajos y Recursos Asignados', en: 'Assigned Tasks & Resources', zh: '已分配任务与资源', ja: '割り当てられた課題と教材', pt: 'Trabalhos e Recursos Atribuídos' },
  'Cargando tus tareas mágicas de primaria...': { es: 'Cargando tus tareas mágicas de primaria...', en: 'Loading your magic primary school tasks...', zh: '正在加载你的魔法任务...', ja: '魔法の課題を読み込み中...', pt: 'Carregando suas tarefas mágicas...' },
  '📝 Tarea': { es: '📝 Tarea', en: '📝 Task', zh: '📝 任务', ja: '📝 課題', pt: '📝 Tarefa' },
  '🎥 Video': { es: '🎥 Video', en: '🎥 Video', zh: '🎥 视频', ja: '🎥 動画', pt: '🎥 Vídeo' },
  '📄 Guía de Lectura': { es: '📄 Guía de Lectura', en: '📄 Reading Guide', zh: '📄 阅读指南', ja: '📄 読み物ガイド', pt: '📄 Guia de Leitura' },
  '📄 Contenido': { es: '📄 Contenido', en: '📄 Content', zh: '📄 内容', ja: '📄 教材', pt: '📄 Conteúdo' },
  '¡Tarea Entregada!': { es: '¡Tarea Entregada!', en: 'Task Submitted!', zh: '任务已提交！', ja: '課題提出完了！', pt: 'Tarefa Entregue!' },
  'Pendiente por Entregar': { es: 'Pendiente por Entregar', en: 'Pending Submission', zh: '待提交', ja: '未提出', pt: 'Pendente para Entrega' },
  '¡Sube tu archivo para ganar puntos!': { es: '¡Sube tu archivo para ganar puntos!', en: 'Upload your file to earn points!', zh: '上传文件以获取积分！', ja: 'ファイルをアップロードしてポイントを獲得しよう！', pt: 'Envie seu arquivo para ganhar pontos!' },
  'Calificación:': { es: 'Calificación:', en: 'Grade:', zh: '成绩：', ja: '成績：', pt: 'Nota:' },
  '💬 Mensaje de tu Profe:': { es: '💬 Mensaje de tu Profe:', en: "💬 Teacher's Note:", zh: '💬 老师评语：', ja: '💬 先生からのメッセージ：', pt: '💬 Recado do Professor:' },
  '🔄 Reenviar o Modificar Archivo': { es: '🔄 Reenviar o Modificar Archivo', en: '🔄 Resubmit or Modify File', zh: '🔄 重新提交或修改文件', ja: '🔄 ファイルの再提出・変更', pt: '🔄 Reenviar ou Modificar Arquivo' },
  '🚀 Entregar Tarea': { es: '🚀 Entregar Tarea', en: '🚀 Submit Task', zh: '🚀 提交任务', ja: '🚀 課題を提出する', pt: '🚀 Entregar Tarefa' },
  '▶️ Reproducir Video': { es: '▶️ Reproducir Video', en: '▶️ Play Video', zh: '▶️ 播放视频', ja: '▶️ 動画を再生', pt: '▶️ Reproduzir Vídeo' },
  '📖 Abrir Documento': { es: '📖 Abrir Documento', en: '📖 Open Document', zh: '📖 打开文件', ja: '📖 資料を開く', pt: '📖 Abrir Documento' },
  '¡Todo al día por aquí!': { es: '¡Todo al día por aquí!', en: 'All up to date here!', zh: '这里全部完成了！', ja: 'すべて完了しています！', pt: 'Tudo em dia por aqui!' },
  'No tienes tareas pendientes ni actividades para esta materia. ¡Gran trabajo!': { es: 'No tienes tareas pendientes ni actividades para esta materia. ¡Gran trabajo!', en: 'You have no pending tasks or activities for this subject. Great job!', zh: '该科目暂无待办任务。做得很棒！', ja: 'この科目の未完了課題はありません。よく頑張りました！', pt: 'Você não tem tarefas pendentes nem atividades para esta matéria. Ótimo trabalho!' },
  'Mis Clases y Matrícula': { es: 'Mis Clases y Matrícula', en: 'My Classes & Enrollment', zh: '我的课程与注册', ja: '受講クラスと登録', pt: 'Minhas Turmas e Matrícula' },
  'Mis Tareas y Recursos': { es: 'Mis Tareas y Recursos', en: 'My Tasks & Resources', zh: '我的任务与资源', ja: '自分の課題と教材', pt: 'Minhas Tarefas e Recursos' },
  'Monitoreo Semáforo': { es: 'Monitoreo Semáforo', en: 'Traffic Light Monitoring', zh: '信号灯监控', ja: 'シグナル監視', pt: 'Monitoramento Semáforo' },
  'Gestión de Alumnos y Matrícula': { es: 'Gestión de Alumnos y Matrícula', en: 'Student & Enrollment Management', zh: '学生与注册管理', ja: '生徒と受講の管理', pt: 'Gestão de Alunos e Matrículas' },
  'Actividades y Calificaciones': { es: 'Actividades y Calificaciones', en: 'Activities & Grading', zh: '活动与评分', ja: '課題と採点', pt: 'Atividades e Notas' }
};

// Palabras individuales frecuentes para traducción inline contextual
const WORDS: Record<string, Record<SupportedLanguage, string>> = {
  'Docentes': { es: 'Docentes', en: 'Teachers', zh: '教师', ja: '教員', pt: 'Professores' },
  'Docente': { es: 'Docente', en: 'Teacher', zh: '教师', ja: '教員', pt: 'Professor' },
  'Alumnos': { es: 'Alumnos', en: 'Students', zh: '学生', ja: '生徒', pt: 'Alunos' },
  'Alumno': { es: 'Alumno', en: 'Student', zh: '学生', ja: '生徒', pt: 'Aluno' },
  'Estudiantes': { es: 'Estudiantes', en: 'Students', zh: '学生', ja: '生徒', pt: 'Estudantes' },
  'Estudiante': { es: 'Estudiante', en: 'Student', zh: '学生', ja: '生徒', pt: 'Estudante' },
  'pendientes': { es: 'pendientes', en: 'pending', zh: '项待办', ja: '件保留中', pt: 'pendentes' },
  'nuevas': { es: 'nuevas', en: 'new', zh: '门新课', ja: '件新着', pt: 'novas' },
  'alumnos': { es: 'alumnos', en: 'students', zh: '名学生', ja: '名生徒', pt: 'alunos' },
  'Materias': { es: 'Materias', en: 'Subjects', zh: '科目', ja: '科目', pt: 'Disciplinas' },
  'Materia': { es: 'Materia', en: 'Subject', zh: '科目', ja: '科目', pt: 'Disciplina' },
  'Cursos': { es: 'Cursos', en: 'Courses', zh: '课程', ja: 'コース', pt: 'Cursos' },
  'Curso': { es: 'Curso', en: 'Course', zh: '课程', ja: 'コース', pt: 'Curso' },
  'Activo': { es: 'Activo', en: 'Active', zh: '活跃', ja: 'アクティブ', pt: 'Ativo' },
  'Suspendido': { es: 'Suspendido', en: 'Suspended', zh: '已停用', ja: '停止中', pt: 'Suspenso' },
  'Habilitada': { es: 'Habilitada', en: 'Enabled', zh: '已启用', ja: '有効', pt: 'Habilitada' },
  'Suspendida': { es: 'Suspendida', en: 'Suspended', zh: '已停用', ja: '停止中', pt: 'Suspensa' }
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

  private observer: MutationObserver | null = null;
  private debounceTimer: any = null;

  constructor() {
    if (typeof document !== 'undefined') {
      const initial = this.currentLang();
      document.documentElement.lang = initial;
      this.syncGoogleCookie(initial);

      // Esperar a que el DOM esté listo para inicializar el observador
      if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => this.initDomObserver());
      } else {
        setTimeout(() => this.initDomObserver(), 100);
      }
    }
  }

  private getInitialLanguage(): SupportedLanguage {
    if (typeof localStorage !== 'undefined') {
      const saved = localStorage.getItem(this.STORAGE_KEY) as SupportedLanguage;
      if (saved && (saved === 'es' || saved === 'en' || saved === 'zh' || saved === 'ja' || saved === 'pt')) {
        return saved;
      }
    }
    if (typeof navigator !== 'undefined' && navigator.language) {
      const prefix = navigator.language.slice(0, 2).toLowerCase() as SupportedLanguage;
      if (prefix === 'en' || prefix === 'zh' || prefix === 'ja' || prefix === 'pt') {
        return prefix;
      }
    }
    return 'es';
  }

  public setLanguage(lang: SupportedLanguage): void {
    this.currentLang.set(lang);
    if (typeof localStorage !== 'undefined') {
      localStorage.setItem(this.STORAGE_KEY, lang);
    }
    if (typeof document !== 'undefined') {
      document.documentElement.lang = lang;
      this.syncGoogleCookie(lang);
      this.triggerGoogleTranslateCombo(lang);
      this.translateDomTree(lang);
    }
  }

  /**
   * Sincroniza la cookie oficial de Google Translate para traducción profunda de toda la página
   */
  private syncGoogleCookie(lang: SupportedLanguage): void {
    if (typeof document === 'undefined') return;
    const googleMap: Record<SupportedLanguage, string> = {
      es: '/es/es',
      en: '/es/en',
      zh: '/es/zh-CN',
      ja: '/es/ja',
      pt: '/es/pt'
    };
    const val = googleMap[lang] || '/es/es';
    const expires = 'path=/; expires=' + new Date(Date.now() + 30 * 86400000).toUTCString();

    document.cookie = `googtrans=${val}; ${expires}`;
    const domain = window.location.hostname;
    document.cookie = `googtrans=${val}; domain=.${domain}; ${expires}`;
    document.cookie = `googtrans=${val}; domain=${domain}; ${expires}`;
  }

  /**
   * Si el widget de Google Translate está disponible en la página, activa su combo box
   */
  private triggerGoogleTranslateCombo(lang: SupportedLanguage): void {
    if (typeof document === 'undefined') return;
    const googleMap: Record<SupportedLanguage, string> = {
      es: 'es',
      en: 'en',
      zh: 'zh-CN',
      ja: 'ja',
      pt: 'pt'
    };
    const target = googleMap[lang];
    const combo = document.querySelector('select.goog-te-combo') as HTMLSelectElement | null;
    if (combo) {
      combo.value = target;
      combo.dispatchEvent(new Event('change'));
    }
  }

  /**
   * Inicializa un MutationObserver global para traducir en tiempo real componentes recién montados
   */
  private initDomObserver(): void {
    if (typeof document === 'undefined' || typeof MutationObserver === 'undefined') return;

    if (this.currentLang() !== 'es') {
      this.translateDomTree(this.currentLang());
    }

    this.observer = new MutationObserver((mutations) => {
      if (this.currentLang() === 'es') return;
      let shouldTranslate = false;
      for (const m of mutations) {
        if (m.type === 'childList' && m.addedNodes.length > 0) {
          shouldTranslate = true;
          break;
        }
      }
      if (shouldTranslate) {
        clearTimeout(this.debounceTimer);
        this.debounceTimer = setTimeout(() => {
          this.translateDomTree(this.currentLang());
        }, 30);
      }
    });

    this.observer.observe(document.body, {
      childList: true,
      subtree: true
    });
  }

  /**
   * Recorre recursivamente el árbol DOM traduciendo textos, placeholders y títulos a velocidad nativa
   */
  public translateDomTree(lang: SupportedLanguage): void {
    if (typeof document === 'undefined') return;

    const walker = document.createTreeWalker(
      document.body,
      NodeFilter.SHOW_TEXT,
      {
        acceptNode: (node: Node) => {
          const parent = node.parentElement;
          if (!parent) return NodeFilter.FILTER_REJECT;
          const tag = parent.tagName.toLowerCase();
          if (tag === 'script' || tag === 'style' || tag === 'noscript' || tag === 'code') {
            return NodeFilter.FILTER_REJECT;
          }
          if (parent.closest('.skiptranslate, app-language-selector, .language-selector-container')) {
            return NodeFilter.FILTER_REJECT;
          }
          const val = node.nodeValue?.trim();
          if (!val || val.length === 0 || /^\d+$/.test(val)) {
            return NodeFilter.FILTER_REJECT;
          }
          return NodeFilter.FILTER_ACCEPT;
        }
      }
    );

    let currentNode = walker.nextNode();
    while (currentNode) {
      this.processTextNode(currentNode as Text, lang);
      currentNode = walker.nextNode();
    }

    // Traducir también placeholders de inputs y textareas
    const inputs = document.querySelectorAll<HTMLInputElement | HTMLTextAreaElement>('input[placeholder], textarea[placeholder]');
    inputs.forEach(input => {
      if (input.closest('app-language-selector')) return;
      if (lang === 'es') {
        const orig = (input as any).__origPlaceholder;
        if (orig) input.placeholder = orig;
      } else {
        const current = input.placeholder.trim();
        if (!(input as any).__origPlaceholder) {
          (input as any).__origPlaceholder = current;
        }
        const orig = (input as any).__origPlaceholder;
        const translated = this.translatePhrase(orig, lang);
        if (translated) {
          input.placeholder = translated;
        }
      }
    });
  }

  private processTextNode(node: Text, lang: SupportedLanguage): void {
    if (lang === 'es') {
      const orig = (node as any).__origValue;
      if (orig !== undefined) {
        node.nodeValue = orig;
      }
      return;
    }

    const currentVal = node.nodeValue;
    if (!currentVal) return;

    if ((node as any).__origValue === undefined) {
      (node as any).__origValue = currentVal;
    }

    const orig = (node as any).__origValue;
    const trimmed = orig.trim();
    if (!trimmed) return;

    const translated = this.translatePhrase(trimmed, lang);
    if (translated && translated !== trimmed) {
      // Preservar espacios al inicio y final
      const leadingMatch = orig.match(/^\s*/);
      const trailingMatch = orig.match(/\s*$/);
      const leading = leadingMatch ? leadingMatch[0] : '';
      const trailing = trailingMatch ? trailingMatch[0] : '';
      node.nodeValue = leading + translated + trailing;
    }
  }

  /**
   * Resuelve la traducción de una frase o texto compuesto
   */
  public translatePhrase(text: string, lang: SupportedLanguage): string {
    if (lang === 'es' || !text) return text;

    // 1. Coincidencia exacta en el diccionario de frases
    if (PHRASES[text]?.[lang]) {
      return PHRASES[text][lang];
    }

    // 2. Extraer emoji prefijo si existe (ej. "➕ Registrar Usuario" -> "➕ " + "Registrar Usuario")
    const emojiMatch = text.match(/^([\p{Emoji}\p{Extended_Pictographic}\s]+)(.+)$/u);
    if (emojiMatch) {
      const prefix = emojiMatch[1];
      const rest = emojiMatch[2].trim();
      if (PHRASES[rest]?.[lang]) {
        return prefix + PHRASES[rest][lang];
      }
    }

    // 3. Diccionario de palabras contextuales (ej. "Docentes", "Alumnos", "pendientes")
    if (WORDS[text]?.[lang]) {
      return WORDS[text][lang];
    }

    // 4. Frases compuestas con números o interpolaciones (ej. "4 Docentes", "12 pendientes", "Total: 5")
    let replaced = text;
    for (const [key, map] of Object.entries(WORDS)) {
      if (replaced.includes(key)) {
        replaced = replaced.split(key).join(map[lang]);
      }
    }
    for (const [key, map] of Object.entries(PHRASES)) {
      if (replaced.includes(key)) {
        replaced = replaced.split(key).join(map[lang]);
      }
    }

    return replaced;
  }

  public translate(key: string, fallback?: string): string {
    const lang = this.currentLang();
    if (lang === 'es') return fallback ?? key;
    if (PHRASES[key]?.[lang]) return PHRASES[key][lang];
    return this.translatePhrase(key, lang) || fallback || key;
  }

  public t(key: string, fallback?: string): string {
    return this.translate(key, fallback);
  }
}
