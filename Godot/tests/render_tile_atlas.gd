extends SceneTree


func _init() -> void:
	var maker := TileArt.new()
	var atlas := Image.create(6 * 80 + 16, 7 * 104 + 16, false, Image.FORMAT_RGBA8)
	atlas.fill(Color("071e1a"))
	for kind in 42:
		var sprite := maker.texture_for(kind, true, false).get_image()
		var column := kind % 6
		var row := kind / 6
		atlas.blit_rect(sprite, Rect2i(0, 0, 72, 96), Vector2i(12 + column * 80, 12 + row * 104))
	var output := ProjectSettings.globalize_path("res://../qa/tile-atlas.png")
	DirAccess.make_dir_recursive_absolute(output.get_base_dir())
	var error := atlas.save_png(output)
	print("Tile atlas: %s (%s)" % [output, error_string(error)])
	quit(0 if error == OK else 1)
