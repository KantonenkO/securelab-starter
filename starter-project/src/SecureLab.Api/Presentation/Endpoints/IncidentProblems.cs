namespace SecureLab.Api.Presentation.Endpoints;

/// <summary>Єдині відповіді відмов (політика docs/access-matrix.md): однакова 404 для чужого й неіснуючого ресурсу.</summary>
public static class IncidentProblems
{
    // Тіло не містить identifier: для чужого й неіснуючого id відрізняється лише traceId.
    public static IResult NotFound() => Results.Problem(
        title: "Інцидент не знайдено", detail: "Ресурс не знайдено або недоступний.", statusCode: StatusCodes.Status404NotFound);

    public static IResult CommentNotFound() => Results.Problem(
        title: "Коментар не знайдено", detail: "Ресурс не знайдено або недоступний.", statusCode: StatusCodes.Status404NotFound);

    public static IResult Forbidden() => Results.Problem(
        title: "Forbidden", detail: "Дію не дозволено.", statusCode: StatusCodes.Status403Forbidden);

    public static IResult InvalidTransition() => Results.Problem(
        title: "Недопустимий перехід", detail: "Перехід можливий лише зі статусу New.", statusCode: StatusCodes.Status409Conflict);
}
