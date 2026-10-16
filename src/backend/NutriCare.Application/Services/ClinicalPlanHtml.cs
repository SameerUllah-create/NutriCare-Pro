namespace NutriCare.Application.Services;

using System.Globalization;
using System.Net;
using System.Text;
using NutriCare.Application.DTOs;

public static class ClinicalPlanHtml
{
    public static string Render(DietPlanDto plan)
    {
        var html = new StringBuilder("""
            <!doctype html><html lang="en"><head><meta charset="utf-8">
            <meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'; font-src data:; img-src data:">
            <title>Nutrition plan</title><style>
            @page{size:A4;margin:16mm 14mm}body{font-family:'Noto Naskh Arabic','Noto Sans',Arial,sans-serif;font-size:11pt;line-height:1.55;color:#15332d}
            h1{font-size:22pt}h2{font-size:16pt;margin-top:22pt}p,dd,td{overflow-wrap:anywhere;white-space:pre-wrap}dt{font-weight:bold}dd{margin:0 0 8pt}
            table{width:100%;border-collapse:collapse;font-size:10pt}caption{text-align:start;font-weight:bold;margin:8pt 0}th,td{border:1px solid #9aacaa;padding:7pt;vertical-align:top;text-align:start}
            thead{display:table-header-group}tr{break-inside:avoid}h1,h2,h3{break-after:avoid}.meta{color:#36534c}.urdu{font-family:'Noto Naskh Arabic',Arial,sans-serif}
            </style></head><body><main>
            """);
        html.Append("<h1>").Append(Text(plan.Title)).Append("</h1><p class='urdu' lang='ur' dir='rtl'>غذائی منصوبہ</p>");
        html.Append("<dl><dt>Version / نسخہ</dt><dd>").Append(plan.Version).Append("</dd>");
        html.Append("<dt>Dates / تاریخیں</dt><dd>").Append(plan.StartDate.ToString("yyyy-MM-dd")).Append(" – ").Append(plan.EndDate.ToString("yyyy-MM-dd")).Append("</dd>");
        html.Append("<dt>Review date / نظر ثانی کی تاریخ</dt><dd>").Append(plan.ReviewDate?.ToString("yyyy-MM-dd") ?? "Not specified").Append("</dd></dl>");
        Paragraph(html, "Objective / مقصد", plan.Objective);
        Paragraph(html, "Description / تفصیل", plan.Description);
        html.Append("<h2>Estimated daily nutrition / روزانہ غذائیت کا تخمینہ</h2><p>")
            .Append(plan.DailyCalories).Append(" kcal; Protein / پروٹین: ").Append(Number(plan.ProteinGrams))
            .Append(" g; Carbohydrate / کاربوہائیڈریٹ: ").Append(Number(plan.CarbsGrams)).Append(" g; Fat / چکنائی: ")
            .Append(Number(plan.FatsGrams)).Append(" g; Fibre / فائبر: ").Append(Number(plan.FiberGrams)).Append(" g</p>");
        html.Append("<p>Water / پانی: ").Append(Number(plan.WaterIntakeLiters)).Append(" L</p>");
        Paragraph(html, "Instructions / ہدایات", plan.Instructions);
        Paragraph(html, "Supplements / سپلیمنٹس", plan.Supplements);
        html.Append("<h2>Meal schedule / کھانوں کا شیڈول</h2><table><caption>Meals and portions / کھانے اور مقدار</caption><thead><tr>")
            .Append("<th scope='col'>Day / دن</th><th scope='col'>Meal / کھانا</th><th scope='col'>Time / وقت</th><th scope='col'>Food and portion / غذا اور مقدار</th><th scope='col'>Estimated nutrition / غذائیت کا تخمینہ</th></tr></thead><tbody>");
        foreach (var meal in plan.Meals.OrderBy(m => m.DayNumber).ThenBy(m => m.MealTime))
        {
            html.Append("<tr><td>").Append(meal.DayNumber).Append("</td><td>").Append(Text(meal.MealType))
                .Append("</td><td>").Append(meal.MealTime?.ToString("HH:mm") ?? "—").Append("</td><td>").Append(Text(meal.ItemName))
                .Append("<br>").Append(Number(meal.Quantity)).Append(' ').Append(Text(meal.Unit));
            if (!string.IsNullOrWhiteSpace(meal.Notes)) html.Append("<br>").Append(Text(meal.Notes));
            html.Append("</td><td>").Append(meal.Calories).Append(" kcal<br>Protein: ").Append(Number(meal.Protein))
                .Append(" g; Carbs: ").Append(Number(meal.Carbs)).Append(" g; Fat: ").Append(Number(meal.Fats))
                .Append(" g; Fibre: ").Append(Number(meal.Fiber)).Append(" g</td></tr>");
        }
        html.Append("</tbody></table>");
        Paragraph(html, "Additional plan notes / اضافی ہدایات", plan.Notes);
        html.Append("<p class='meta'>Nutrition values are estimates. Follow the instructions reviewed by your nutrition professional.</p>")
            .Append("<p class='urdu meta' lang='ur' dir='rtl'>غذائی مقداریں تخمینی ہیں۔ اپنے ماہر غذائیت کی منظور شدہ ہدایات پر عمل کریں۔</p></main></body></html>");
        return html.ToString();
    }

    private static void Paragraph(StringBuilder html, string heading, string? text)
    {
        if (!string.IsNullOrWhiteSpace(text)) html.Append("<h2>").Append(WebUtility.HtmlEncode(heading)).Append("</h2><p>").Append(Text(text)).Append("</p>");
    }
    private static string Number(decimal? value) => value?.ToString("0.##", CultureInfo.InvariantCulture) ?? "—";
    private static string Text(string? value)
    {
        var text = value ?? "";
        var language = text.Any(c => c is >= '\u0600' and <= '\u06ff') ? "ur" : "en";
        return $"<span dir='auto' lang='{language}'>{WebUtility.HtmlEncode(text)}</span>";
    }
}
