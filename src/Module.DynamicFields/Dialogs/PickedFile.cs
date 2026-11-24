namespace soft.square.net.Module.DynamicFields.Dialogs;

/// <summary>
/// A file as the Storage module's file browser (Module.Storage/pick-files) answers it: <c>{ id, name, size, contentType,
/// version, folderId, publicUrl }</c>. <see cref="PublicUrl"/> is set when the file is in a public folder.
/// </summary>
public sealed record PickedFile(string Id, string Name, long? Size = null, string? ContentType = null, string? PublicUrl = null);
