param(
    [string] $PropertiesDirectory = (Join-Path $PSScriptRoot '..\BlueH\Properties'),
    [double] $LengthIncrease = 0.4
)

$accented = @{
    [char]'a' = [char]0x00E1; [char]'e' = [char]0x00E9; [char]'i' = [char]0x00ED; [char]'o' = [char]0x00F6
    [char]'u' = [char]0x00FC; [char]'c' = [char]0x00E7; [char]'n' = [char]0x00F1; [char]'y' = [char]0x00FD
    [char]'A' = [char]0x00C5; [char]'E' = [char]0x00C9; [char]'I' = [char]0x00CE; [char]'O' = [char]0x00D6
    [char]'U' = [char]0x00DC; [char]'C' = [char]0x00C7; [char]'N' = [char]0x00D1
}
$padding = [char]0x00B7

function ConvertTo-Pseudo([string] $text) {
    $parts = [regex]::Split($text, '(\{\d+(?::[^}]*)?\})')
    $builder = [Text.StringBuilder]::new('[')
    foreach ($part in $parts) {
        if ($part -match '^\{\d+(?::[^}]*)?\}$') { [void]$builder.Append($part); continue }
        foreach ($character in $part.ToCharArray()) {
            if ($accented.ContainsKey($character)) { [void]$builder.Append($accented[$character]) }
            else { [void]$builder.Append($character) }
        }
    }
    [void]$builder.Append([string]::new($padding, [math]::Ceiling($text.Length * $LengthIncrease)))
    [void]$builder.Append(']')
    $builder.ToString()
}

$neutralPath = Join-Path $PropertiesDirectory 'Resources.resx'
$neutral = [IO.File]::ReadAllText($neutralPath)
[xml]$document = $neutral
$headerEnd = $neutral.LastIndexOf('</resheader>') + '</resheader>'.Length + 1
$builder = [Text.StringBuilder]::new($neutral.Substring(0, $headerEnd))

foreach ($data in $document.root.data) {
    $value = [Security.SecurityElement]::Escape((ConvertTo-Pseudo $data.value))
    [void]$builder.Append("  <data name=`"$($data.name)`" xml:space=`"preserve`">`n    <value>$value</value>`n  </data>`n")
}

[void]$builder.Append("</root>`n")
$target = Join-Path $PropertiesDirectory 'Resources.qps-ploc.resx'
[IO.File]::WriteAllText($target, $builder.ToString(), [Text.UTF8Encoding]::new($false))
"wrote $target"
