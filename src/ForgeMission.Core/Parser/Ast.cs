namespace ForgeMission.Core.Parser;

public record Program(IReadOnlyList<Declaration> Declarations);

public abstract record Declaration(string Name);

public record MissionDeclaration(string Name, Pipeline Pipeline) : Declaration(Name);

public record ExpertDeclaration(string Name, Pipeline Pipeline) : Declaration(Name);

public record Pipeline(IReadOnlyList<string> Steps);
