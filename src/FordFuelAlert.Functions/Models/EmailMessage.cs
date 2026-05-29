namespace FordFuelAlert.Functions.Models;

public sealed record EmailMessage(string To, string Subject, string Body);
