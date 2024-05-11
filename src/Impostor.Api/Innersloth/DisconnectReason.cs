namespace Impostor.Api.Innersloth
{
    public enum DisconnectReason
    {
        ExitGame = 0,
        GameFull = 1,
        GameStarted = 2,
        GameNotFound = 3,
        IncorrectVersion = 5,
        Banned = 6,
        Kicked = 7,
        Custom = 8,
        InvalidName = 9,
        Destroy = 16,
        Error = 17,
        IncorrectGame = 18,
        ServerRequest = 19,
        ServerFull = 20,
        IntentionalLeaving = 208,
        FocusLostBackground = 207,
        FocusLost = 209,
        NewConnection = 210,
        MapWontExist = 211,
        KickedHacking = 212,
        DifferentMods = 213,
    }
}
