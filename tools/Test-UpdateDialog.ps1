param(
    [Parameter(Mandatory=$true)][string]$ExePath,
    [string]$NamespaceName = 'SoopPinballCollector',
    [Parameter(Mandatory=$true)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
[Windows.Forms.Application]::SetCompatibleTextRenderingDefault($false)
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $ExePath).Path)
$type = $assembly.GetType("$NamespaceName.UpdatePromptDialog", $true)
$flags = [Reflection.BindingFlags]'Instance,Public,NonPublic'
$notes = @(
    '창 크기에 따른 버튼 잘림, 큰 코인 합계 표시, 목록 편집 입력칸 정렬을 개선했습니다. 기존 문구·글꼴·수집 및 코인 규칙은 유지합니다.',
    ('업데이트 내용을 확인합니다. ' * 20).Substring(0, 180),
    'Update test notes'
)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$results = @()
for ($index=0; $index -lt $notes.Count; $index++) {
    $dialog = [Activator]::CreateInstance($type, $flags, $null, @([Version]'1.4.4.5', $notes[$index]), $null)
    try {
        $dialog.StartPosition = 'Manual'
        $dialog.Location = New-Object Drawing.Point(-20000, -20000)
        $dialog.Show()
        [Windows.Forms.Application]::DoEvents()
        $caption = $dialog.Controls['caption']
        $title = $dialog.Controls['title']
        $version = $dialog.Controls['version']
        $body = $dialog.Controls['notes']
        $later = $dialog.Controls['laterButton']
        $update = $dialog.Controls['updateButton']
        foreach ($control in $dialog.Controls) {
            if (!$dialog.ClientRectangle.Contains($control.Bounds)) { throw "Outside dialog: $($control.Name)" }
            $size = [Windows.Forms.TextRenderer]::MeasureText($control.Text, $control.Font,
                (New-Object Drawing.Size($control.Width, [int]::MaxValue)),
                [Windows.Forms.TextFormatFlags]'NoPadding,NoPrefix,WordBreak')
            if ($size.Height -gt $control.Height) { throw "Clipped text: $($control.Name)" }
        }
        if ($caption.Left -ne $title.Left -or $title.Left -ne $version.Left -or $version.Left -ne $body.Left) { throw 'Left edges differ' }
        if ($dialog.ClientSize.Width - $update.Right -ne $body.Left) { throw 'Right margin differs' }
        if ($update.Size -ne $later.Size -or $update.Top -ne $later.Top) { throw 'Button alignment differs' }
        if ($later.Top - $body.Bottom -lt 20) { throw 'Notes overlap buttons' }
        if ($body.Text -cne $notes[$index]) { throw 'Notes were modified' }
        if ($title.Text -cne '새 버전을 사용할 수 있습니다') { throw 'Title changed' }
        if ($later.Text -cne '나중에' -or $update.Text -cne '업데이트') { throw 'Button wording changed' }
        if ($dialog.AcceptButton -ne $update -or $dialog.CancelButton -ne $later) { throw 'Keyboard bindings changed' }
        if ($title.Font.Size -ne 13 -or $later.Font.Size -ne 10 -or $body.Font.Size -ne 10) { throw 'Unexpected font size' }
        $bitmap = New-Object Drawing.Bitmap($dialog.ClientSize.Width, $dialog.ClientSize.Height)
        try {
            $dialog.DrawToBitmap($bitmap, $dialog.ClientRectangle)
            $bitmap.Save((Join-Path $OutputDirectory "prompt-$index.png"), [Drawing.Imaging.ImageFormat]::Png)
        } finally { $bitmap.Dispose() }
        $later.PerformClick()
        if ($dialog.DialogResult -ne [Windows.Forms.DialogResult]::Cancel) { throw 'Later button failed' }
        $update.PerformClick()
        if ($dialog.DialogResult -ne [Windows.Forms.DialogResult]::OK) { throw 'Update button failed' }
        $results += [pscustomobject]@{NoteLength=$body.Text.Length;Client=$dialog.ClientSize.ToString();Left=$body.Left;Right=$dialog.ClientSize.Width-$update.Right;ButtonGap=$update.Left-$later.Right;NotesHeight=$body.Height;Font=$body.Font.Name}
    } finally { $dialog.Dispose() }
}
$results | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputDirectory 'dialog.json') -Encoding UTF8
Write-Output "PASS: $NamespaceName dialog, 3 note lengths, alignment, clipping, fonts and actions"
