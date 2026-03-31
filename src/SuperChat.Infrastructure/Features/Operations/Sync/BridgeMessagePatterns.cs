namespace SuperChat.Infrastructure.Features.Operations.Sync;

internal static class BridgeMessagePatterns
{
    public static readonly string[] SuccessfulLoginIndicators = ["logged in as", "login successful", "successfully logged in"];
    public static readonly string[] NotLoggedInIndicators = ["not logged in"];
    public static readonly string[] LostConnectionIndicators = ["not logged in", "logged out"];

    public static readonly string[] PasswordStepIndicators = ["two-step verification", "2fa"];
    public static readonly string PasswordSendPhrase = "send your password";
    public static readonly string[] CodeStepIndicators = ["verification code", "code to the bot", "enter the code", "code here"];
    public static readonly string[] PhoneStepIndicators = ["phone number"];
    public static readonly string[] PhoneLoginIndicators = ["log in"];

    public static readonly string[] GreetingIndicators = ["telegram bridge bot"];
    public static readonly string GreetingHelloPhrase = "hello";
    public static readonly string GreetingBridgePhrase = "telegram bridge";
}
