using System.Text.Json;

namespace RigMatch.Api.Models;

public sealed record SaveCompanyCvRequest(JsonElement FinalProfile);
