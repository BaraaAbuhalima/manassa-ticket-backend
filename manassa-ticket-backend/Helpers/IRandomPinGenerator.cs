namespace manassa_ticket_backend.Helpers;

public interface IRandomPinGenerator
{
    string Generate(int length);
}