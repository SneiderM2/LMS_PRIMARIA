namespace LMS.API.Entities;

public enum SemaforoColor
{
    Green,   // 🟢 Verde: Ingresó hoy (Días transcurridos == 0)
    Yellow,  // 🟡 Amarillo: Entre 2 y 4 días sin ingresar (o 1 día en alerta leve)
    Red      // 🔴 Rojo: 5 o más días sin ingresar (o sin registros)
}

public class SemaforoInfo
{
    public SemaforoColor Color { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Emoji { get; set; } = string.Empty;
    public int InactiveDays { get; set; }
    public string Description { get; set; } = string.Empty;

    public static SemaforoInfo Calculate(DateTime? lastLoginDate)
    {
        if (!lastLoginDate.HasValue)
        {
            return new SemaforoInfo
            {
                Color = SemaforoColor.Red,
                Label = "Sin ingresos registrados",
                Emoji = "🔴",
                InactiveDays = 999,
                Description = "El estudiante aún no ha iniciado sesión en la plataforma."
            };
        }

        // Comparar días enteros considerando fechas UTC
        var today = DateTime.UtcNow.Date;
        var loginDate = lastLoginDate.Value.ToUniversalTime().Date;
        var daysDiff = (int)(today - loginDate).TotalDays;

        if (daysDiff < 0) daysDiff = 0; // Por sincronización de relojes

        if (daysDiff == 0)
        {
            return new SemaforoInfo
            {
                Color = SemaforoColor.Green,
                Label = "Activo Hoy",
                Emoji = "🟢",
                InactiveDays = 0,
                Description = "¡Excelente! El estudiante ingresó hoy a clases."
            };
        }
        else if (daysDiff >= 1 && daysDiff <= 4)
        {
            // Nota: El requerimiento especifica: 2 a 4 días amarillo. 
            // Si es 1 día (ayer), se muestra como amarillo preventivo para que el docente esté alerta.
            return new SemaforoInfo
            {
                Color = SemaforoColor.Yellow,
                Label = daysDiff == 1 ? "Inactivo desde ayer" : $"Inactivo ({daysDiff} días)",
                Emoji = "🟡",
                InactiveDays = daysDiff,
                Description = $"Atención: Lleva {daysDiff} día(s) sin ingresar al aula virtual."
            };
        }
        else
        {
            // 5 o más días
            return new SemaforoInfo
            {
                Color = SemaforoColor.Red,
                Label = $"Riesgo ({daysDiff} días)",
                Emoji = "🔴",
                InactiveDays = daysDiff,
                Description = $"¡Alerta! Lleva {daysDiff} días ausente. Requiere seguimiento docente."
            };
        }
    }
}
