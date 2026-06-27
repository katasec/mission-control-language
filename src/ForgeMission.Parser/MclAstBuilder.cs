using Antlr4.Runtime;

namespace ForgeMission.Parser;

internal class MclAstBuilder : MclGrammarBaseVisitor<object?>
{
    private static SourceSpan SpanOf(ParserRuleContext ctx) => new(
        ctx.Start.Line,
        ctx.Start.Column,
        ctx.Stop?.Line   ?? ctx.Start.Line,
        (ctx.Stop?.Column ?? ctx.Start.Column) + (ctx.Stop?.Text.Length ?? ctx.Start.Text.Length));

    public override object? VisitProgram(MclGrammarParser.ProgramContext ctx)
    {
        var bindings = ctx.letBinding()
            .Select(b => (LetBinding)Visit(b)!)
            .ToList();

        var declarations = ctx.declaration()
            .Select(d => (Declaration)Visit(d)!)
            .ToList();

        var outputs = ctx.outputDecl()
            .Select(o => (OutputDeclaration)Visit(o)!)
            .ToList();

        return new Program(bindings, declarations, outputs, SpanOf(ctx));
    }

    public override object? VisitLetBinding(MclGrammarParser.LetBindingContext ctx)
    {
        var name  = ctx.LOWER_ID().GetText();
        var value = ParseLetValue(ctx.value());
        return new LetBinding(name, value, SpanOf(ctx));
    }

    public override object? VisitOutputDecl(MclGrammarParser.OutputDeclContext ctx)
    {
        var missionName = ctx.UPPER_ID().GetText();
        var filePath    = ctx.STRING() is { } s ? StripQuotes(s.GetText()) : null;
        return new OutputDeclaration(missionName, filePath, SpanOf(ctx));
    }

    public override object? VisitDeclaration(MclGrammarParser.DeclarationContext ctx)
    {
        if (ctx.mission() is { } m) return Visit(m);
        throw new ParseException("Unknown declaration", ctx.Start.Line, ctx.Start.Column);
    }

    public override object? VisitMission(MclGrammarParser.MissionContext ctx)
    {
        var name     = ctx.UPPER_ID().GetText();
        var @params  = ParseParams(ctx.@params());
        var pipeline = (Pipeline)Visit(ctx.pipeline())!;
        var maxLoops = ctx.loopClause() is { } lc ? int.Parse(lc.INT().GetText()) : 1;
        return new MissionDeclaration(name, @params, pipeline, maxLoops, SpanOf(ctx));
    }

    public override object? VisitPipeline(MclGrammarParser.PipelineContext ctx)
    {
        var elements = ctx.pipelineElement()
            .Select(e => (PipelineElement)Visit(e)!)
            .ToList();
        return new Pipeline(elements, SpanOf(ctx));
    }

    public override object? VisitPipelineElement(MclGrammarParser.PipelineElementContext ctx)
    {
        if (ctx.step() is { } s)         return new StepElement((Step)Visit(s)!);
        if (ctx.parallelBlock() is { } p) return Visit(p);
        if (ctx.debateBlock() is { })
            throw new ParseException(
                "debate {} is not yet implemented — use parallel {} for multi-agent fan-out. debate is planned for a future release.",
                ctx.Start.Line, ctx.Start.Column);
        throw new ParseException("Unknown pipeline element", ctx.Start.Line, ctx.Start.Column);
    }

    public override object? VisitStep(MclGrammarParser.StepContext ctx)
    {
        var name    = ctx.UPPER_ID().GetText();
        var context = ctx.contextClause() is { } cc
            ? (IReadOnlyList<Binding>)Visit(cc)!
            : (IReadOnlyList<Binding>)[];
        var @using  = ctx.usingClause() is { } uc ? (string)Visit(uc)! : null;
        var when    = ctx.whenClause() is { } wc ? (WhenClause)Visit(wc)! : null;
        return new Step(name, context, @using, when, SpanOf(ctx));
    }

    public override object? VisitContextClause(MclGrammarParser.ContextClauseContext ctx)
    {
        var bindings = ctx.binding().Select(b => (Binding)Visit(b)!).ToList();
        return (IReadOnlyList<Binding>)bindings;
    }

    public override object? VisitUsingClause(MclGrammarParser.UsingClauseContext ctx)
        => ctx.LOWER_ID().GetText();

    public override object? VisitWhenClause(MclGrammarParser.WhenClauseContext ctx)
        => Visit(ctx.whenExpr());

    public override object? VisitStringEquals(MclGrammarParser.StringEqualsContext ctx)
    {
        var key   = ctx.anyKey().GetText();
        var value = StripQuotes(ctx.STRING().GetText());
        return new StringEqualsWhen(key, value);
    }

    public override object? VisitNumericCompare(MclGrammarParser.NumericCompareContext ctx)
    {
        var key       = ctx.anyKey().GetText();
        var threshold = double.Parse(ctx.number().GetText(), System.Globalization.CultureInfo.InvariantCulture);
        var op = ctx.compOp().GetText() switch
        {
            ">"  => CompOp.Gt,
            "<"  => CompOp.Lt,
            ">=" => CompOp.Gte,
            "<=" => CompOp.Lte,
            "==" => CompOp.Eq,
            var s => throw new ParseException($"Unknown comparison operator '{s}'", ctx.Start.Line, ctx.Start.Column)
        };
        return new NumericCompareWhen(key, op, threshold);
    }

    public override object? VisitElseExpr(MclGrammarParser.ElseExprContext ctx)
        => new ElseWhen();

    public override object? VisitParallelBlock(MclGrammarParser.ParallelBlockContext ctx)
    {
        var steps = ctx.step().Select(s => (Step)Visit(s)!).ToList();
        return new ParallelElement(steps, SpanOf(ctx));
    }

    public override object? VisitBinding(MclGrammarParser.BindingContext ctx)
    {
        var key   = ctx.anyKey().GetText();
        var value = ParseBindingValue(ctx.value());
        return new Binding(key, value);
    }

    private static IReadOnlyList<string> ParseParams(MclGrammarParser.ParamsContext? ctx)
        => ctx is null
            ? []
            : ctx.LOWER_ID().Select(id => id.GetText()).ToList();

    private static LetValue ParseLetValue(MclGrammarParser.ValueContext ctx)
    {
        if (ctx.STRING() is { } str)
            return new StringLetValue(StripQuotes(str.GetText()));
        if (ctx.envCall() is { } env)
            return ParseEnvLetValue(env);
        throw new ParseException(
            "let bindings only support string literals and env() calls",
            ctx.Start.Line, ctx.Start.Column);
    }

    private static BindingValue ParseBindingValue(MclGrammarParser.ValueContext ctx)
    {
        if (ctx.STRING() is { } str)
            return new StringBindingValue(StripQuotes(str.GetText()));
        if (ctx.envCall() is { } env)
        {
            var ev = ParseEnvLetValue(env);
            return new EnvBindingValue(ev.VarName, ev.DefaultValue);
        }
        if (ctx.INT() is { } num)
            return new NumberBindingValue(int.Parse(num.GetText()));
        if (ctx.LOWER_ID() is { } id)
            return new VarRefBindingValue(id.GetText());
        throw new ParseException("Unknown value form", ctx.Start.Line, ctx.Start.Column);
    }

    private static EnvLetValue ParseEnvLetValue(MclGrammarParser.EnvCallContext ctx)
    {
        var strings      = ctx.STRING();
        var varName      = StripQuotes(strings[0].GetText());
        var defaultValue = strings.Length > 1 ? StripQuotes(strings[1].GetText()) : null;
        return new EnvLetValue(varName, defaultValue);
    }

    private static string StripQuotes(string text) => text[1..^1];
}
