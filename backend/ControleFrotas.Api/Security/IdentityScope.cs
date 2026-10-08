namespace ControleFrotas;

public static class IdentityScope
{
    public static int Company(HttpContext context) => int.Parse(context.User.FindFirst("company")!.Value);
}
