using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;


namespace MaksIT.Dapr.PubSub;

public enum DaprPubSubAcceptOutcome {
  Accepted = 0,
  AlreadyHandled = 1,
  Busy = 2,
  Rejected = 3,
}

public sealed record DaprPubSubAcceptResult(DaprPubSubAcceptOutcome Outcome, string? Detail = null) {
  public static DaprPubSubAcceptResult Accepted(string? detail = null) => new(DaprPubSubAcceptOutcome.Accepted, detail);
  public static DaprPubSubAcceptResult AlreadyHandled(string? detail = null) => new(DaprPubSubAcceptOutcome.AlreadyHandled, detail);
  public static DaprPubSubAcceptResult Busy(string? detail = null) => new(DaprPubSubAcceptOutcome.Busy, detail);
  public static DaprPubSubAcceptResult Rejected(string? detail = null) => new(DaprPubSubAcceptOutcome.Rejected, detail);
}

/// <summary>Product implements accept/claim; HTTP layer maps to Dapr ACK/NAK via <see cref="DaprPubSubAck"/>.</summary>
public interface IDaprPubSubWorkHandler<TMessage> {
  Task<DaprPubSubAcceptResult> TryAcceptAsync(TMessage message, CancellationToken cancellationToken = default);
}

/// <summary>Maps accept outcomes to HTTP status codes for Dapr pub/sub delivery.</summary>
public static class DaprPubSubAck {
  public static IActionResult ToActionResult(DaprPubSubAcceptResult result) =>
    result.Outcome switch {
      DaprPubSubAcceptOutcome.Accepted => new OkObjectResult(new { status = "accepted", detail = result.Detail }),
      DaprPubSubAcceptOutcome.AlreadyHandled => new OkObjectResult(new { status = "alreadyHandled", detail = result.Detail }),
      DaprPubSubAcceptOutcome.Busy => new ObjectResult(new { status = "busy", detail = result.Detail }) { StatusCode = StatusCodes.Status503ServiceUnavailable },
      DaprPubSubAcceptOutcome.Rejected => new BadRequestObjectResult(new { status = "rejected", detail = result.Detail }),
      _ => new StatusCodeResult(StatusCodes.Status500InternalServerError),
    };

  public static Microsoft.AspNetCore.Http.IResult ToHttpResult(DaprPubSubAcceptResult result) =>
    result.Outcome switch {
      DaprPubSubAcceptOutcome.Accepted => Microsoft.AspNetCore.Http.Results.Ok(new { status = "accepted", detail = result.Detail }),
      DaprPubSubAcceptOutcome.AlreadyHandled => Microsoft.AspNetCore.Http.Results.Ok(new { status = "alreadyHandled", detail = result.Detail }),
      DaprPubSubAcceptOutcome.Busy => Microsoft.AspNetCore.Http.Results.Json(new { status = "busy", detail = result.Detail }, statusCode: StatusCodes.Status503ServiceUnavailable),
      DaprPubSubAcceptOutcome.Rejected => Microsoft.AspNetCore.Http.Results.BadRequest(new { status = "rejected", detail = result.Detail }),
      _ => Microsoft.AspNetCore.Http.Results.StatusCode(StatusCodes.Status500InternalServerError),
    };
}
