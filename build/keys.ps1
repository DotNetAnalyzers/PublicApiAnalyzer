# Note: these values may only change during major release

If ($Version.Contains('-')) {

	# Use the development keys
	$Keys = @{
		'netstandard1.1' = '4d7c3a889f597bf5'
	}

} Else {

	# Use the final release keys
	$Keys = @{
		'netstandard1.1' = '7f89516dec9aea59'
	}

}

function Resolve-FullPath() {
	param([string]$Path)
	[System.IO.Path]::GetFullPath((Join-Path (pwd) $Path))
}
