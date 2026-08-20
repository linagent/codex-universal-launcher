param(
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\assets\brand\codex-universal-launcher-compact.ico')
)

$brandDirectory = Join-Path $PSScriptRoot '..\assets\brand'
$sources = @(
    @{ Size = 32; Path = Join-Path $brandDirectory 'icon-primary-32.png' },
    @{ Size = 64; Path = Join-Path $brandDirectory 'icon-primary-64.png' },
    @{ Size = 128; Path = Join-Path $brandDirectory 'icon-primary-128.png' }
)

$images = foreach ($source in $sources) {
    [pscustomobject]@{
        Size = [int]$source.Size
        Bytes = [System.IO.File]::ReadAllBytes([string]$source.Path)
    }
}

$resolvedOutput = [System.IO.Path]::GetFullPath($OutputPath)
$outputDirectory = [System.IO.Path]::GetDirectoryName($resolvedOutput)
[System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null

$stream = [System.IO.File]::Create($resolvedOutput)
$writer = [System.IO.BinaryWriter]::new($stream)
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$images.Count)

    $offset = 6 + 16 * $images.Count
    foreach ($image in $images) {
        $writer.Write([byte]$image.Size)
        $writer.Write([byte]$image.Size)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$image.Bytes.Length)
        $writer.Write([uint32]$offset)
        $offset += $image.Bytes.Length
    }

    foreach ($image in $images) {
        $writer.Write([byte[]]$image.Bytes)
    }
}
finally {
    $writer.Dispose()
}

Get-Item -LiteralPath $resolvedOutput
