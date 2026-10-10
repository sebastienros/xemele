# Strict single-byte fixtures

EncodingTests contains an independent decimal Windows-1252 oracle (all 32 control slots), exhaustively checking all 256 input bytes for each advertised encoding. Generated document fixtures use literal single-byte body values and ASCII declarations, never a provider encoder. Every public transport split and body output-capacity split is checked. No optional aliases are advertised.
