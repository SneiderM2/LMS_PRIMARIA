using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LMS.API.Migrations;

/// <summary>
/// Migración para soporte legal de Habeas Data y Protección de Menores en Colombia
/// (Ley 1581 de 2012, Decreto 1377 de 2013 y Ley 1098 de 2006).
/// Crea las tablas <c>consentimientos_habeas_data</c> y <c>solicitudes_arco</c>.
/// </summary>
public partial class AddConsentimientoHabeasDataAndSolicitudArco : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "consentimientos_habeas_data",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                alumno_id = table.Column<int>(type: "integer", nullable: false),
                usuario_id = table.Column<int>(type: "integer", nullable: true),
                acudiente_nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                acudiente_documento = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                tipo_documento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "CC"),
                version_politica = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                hash_texto_politica = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                autoriza_servicio_educativo = table.Column<bool>(type: "boolean", nullable: false),
                autoriza_datos_opcionales = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                autoriza_transmision_internacional = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                ip_origen = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                user_agent = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                fecha_autorizacion_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                fecha_revocatoria_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_consentimientos_habeas_data", x => x.id);
                table.ForeignKey(
                    name: "fk_consentimientos_alumnos",
                    column: x => x.alumno_id,
                    principalTable: "alumnos",
                    principalColumn: "usuario_id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_consentimientos_usuarios",
                    column: x => x.usuario_id,
                    principalTable: "usuarios",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "solicitudes_arco",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                usuario_id = table.Column<int>(type: "integer", nullable: true),
                alumno_id = table.Column<int>(type: "integer", nullable: true),
                solicitante_nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                solicitante_documento = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                tipo_solicitud = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                descripcion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                estado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "RADICADA"),
                fecha_solicitud_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                fecha_limite_legal_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                fecha_respuesta_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                respuesta = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                ip_origen = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_solicitudes_arco", x => x.id);
                table.ForeignKey(
                    name: "fk_solicitudes_arco_alumnos",
                    column: x => x.alumno_id,
                    principalTable: "alumnos",
                    principalColumn: "usuario_id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "fk_solicitudes_arco_usuarios",
                    column: x => x.usuario_id,
                    principalTable: "usuarios",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "idx_consentimiento_alumno",
            table: "consentimientos_habeas_data",
            column: "alumno_id");

        migrationBuilder.CreateIndex(
            name: "idx_consentimiento_alumno_vigente",
            table: "consentimientos_habeas_data",
            columns: new[] { "alumno_id", "fecha_revocatoria_utc" });

        migrationBuilder.CreateIndex(
            name: "idx_solicitudes_arco_usuario",
            table: "solicitudes_arco",
            column: "usuario_id");

        migrationBuilder.CreateIndex(
            name: "idx_solicitudes_arco_estado_limite",
            table: "solicitudes_arco",
            columns: new[] { "estado", "fecha_limite_legal_utc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "consentimientos_habeas_data");
        migrationBuilder.DropTable(name: "solicitudes_arco");
    }
}
