namespace Rooby.Engine.Cel;

/// <summary>CEL compilation mode (SPEC §5.1).</summary>
public enum CelCompileMode
{
    /// <summary>Typed <c>input</c> from a Schema.Definition; used at save/publish time and the editor test panel.</summary>
    Checked,

    /// <summary><c>input: dyn</c>; used by the runner and the delivery <c>evaluate</c> endpoint.</summary>
    Dynamic,
}
