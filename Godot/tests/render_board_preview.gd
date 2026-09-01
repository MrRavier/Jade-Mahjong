extends SceneTree


func _init() -> void:
	var game := MahjongCore.new()
	game.build_board(20260901)
	var layout := BoardLayout.new()
	var size := Vector2(940, 508)
	layout.rebuild(size, game.tiles)
	var image := Image.create(int(size.x), int(size.y), false, Image.FORMAT_RGBA8)
	image.fill(Color("061b18"))
	var maker := TileArt.new()
	for tile_id in layout.draw_order:
		var tile: Dictionary = game.tiles[tile_id]
		var rect: Rect2 = layout.rects[tile_id]
		var sprite := maker.texture_for(tile.kind, game.is_free(tile_id), false).get_image()
		sprite.resize(roundi(rect.size.x), roundi(rect.size.y), Image.INTERPOLATE_NEAREST)
		image.blend_rect(sprite, Rect2i(Vector2i.ZERO, sprite.get_size()), Vector2i(rect.position.round()))
	var output := ProjectSettings.globalize_path("res://../qa/regression-orientation-touch/board-preview.png")
	DirAccess.make_dir_recursive_absolute(output.get_base_dir())
	var error := image.save_png(output)
	print("Board preview: %s (%s)" % [output, error_string(error)])
	quit(0 if error == OK else 1)
