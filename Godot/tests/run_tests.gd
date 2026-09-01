extends SceneTree

var failures: Array[String] = []
var checks := 0


func _init() -> void:
	print("Jade Mahjong — native rules test suite")
	_test_layout_and_distribution()
	_test_determinism()
	_test_legal_full_solutions()
	_test_matching_rules()
	_test_room_codes()
	_test_tile_sprite_pipeline()
	_test_orientation_and_scaling()
	_test_board_geometry_and_touch_targets()
	if failures.is_empty():
		print("PASS: %d checks" % checks)
		quit(0)
	else:
		for failure in failures:
			push_error(failure)
		print("FAIL: %d of %d checks" % [failures.size(), checks])
		quit(1)


func _check(condition: bool, message: String) -> void:
	checks += 1
	if not condition:
		failures.append(message)


func _test_layout_and_distribution() -> void:
	var game := MahjongCore.new()
	game.build_board(20260831)
	_check(game.tiles.size() == 144, "The board must contain 144 tiles")
	_check(game.remaining == 144, "Remaining counter must start at 144")
	_check(game.solution.size() == 72, "The generator must produce 72 legal pairs")
	var counts: Dictionary = {}
	for tile in game.tiles:
		counts[tile.kind] = counts.get(tile.kind, 0) + 1
	for kind in 34:
		_check(counts.get(kind, 0) == 4, "Regular kind %d must appear four times" % kind)
	for kind in range(34, 42):
		_check(counts.get(kind, 0) == 1, "Bonus kind %d must appear once" % kind)


func _test_determinism() -> void:
	var first := MahjongCore.new()
	var second := MahjongCore.new()
	var third := MahjongCore.new()
	first.build_board(774411)
	second.build_board(774411)
	third.build_board(774412)
	_check(first.layout_signature() == second.layout_signature(), "Equal seeds must produce equal boards")
	_check(first.layout_signature() != third.layout_signature(), "Different seeds should produce different boards")


func _test_legal_full_solutions() -> void:
	for seed in [1, 7, 99, 7777, 8675309, 2147483000]:
		var game := MahjongCore.new()
		game.build_board(seed)
		for pair in game.solution:
			_check(game.is_free(pair.x), "Seed %d: first tile in solution must be free" % seed)
			_check(game.is_free(pair.y), "Seed %d: second tile in solution must be free" % seed)
			_check(game.try_remove(pair.x, pair.y), "Seed %d: solution pair must match" % seed)
		_check(game.remaining == 0, "Seed %d must be completely solvable" % seed)


func _test_matching_rules() -> void:
	var game := MahjongCore.new()
	game.build_board(12345)
	game.tiles[0].kind = 34
	game.tiles[1].kind = 37
	game.tiles[2].kind = 38
	game.tiles[3].kind = 41
	game.tiles[4].kind = 9
	game.tiles[5].kind = 10
	_check(game.matches(0, 1), "All flowers must match each other")
	_check(game.matches(2, 3), "All seasons must match each other")
	_check(not game.matches(0, 2), "Flowers must not match seasons")
	_check(not game.matches(4, 5), "Different regular tiles must not match")


func _test_room_codes() -> void:
	var network := LanSession.new()
	for address in ["192.168.0.42", "192.168.1.199", "10.0.0.7", "172.20.4.88"]:
		var code := network.encode_room_code(address)
		_check(code.length() == 9 and code[4] == "-", "Room code must use XXXX-XXXX format")
		_check(network.decode_room_code(code) == address, "Room code must recover %s" % address)
	_check(network.decode_room_code("BAD!-CODE").is_empty(), "Invalid room codes must be rejected")
	network.free()


func _test_tile_sprite_pipeline() -> void:
	var sprites := TileArt.new()
	for kind in 42:
		var texture := sprites.texture_for(kind, true, false)
		_check(texture != null, "Kind %d must have a generated sprite" % kind)
		_check(texture.get_width() == 72 and texture.get_height() == 96, "Kind %d sprite must use the canonical size" % kind)


func _test_orientation_and_scaling() -> void:
	_check(ProjectSettings.get_setting("display/window/handheld/orientation") == DisplayServer.SCREEN_LANDSCAPE, "Android must start in landscape")
	_check(ProjectSettings.get_setting("display/window/size/viewport_width") > ProjectSettings.get_setting("display/window/size/viewport_height"), "Base viewport must be horizontal")
	_check(ProjectSettings.get_setting("display/window/stretch/aspect") == "expand", "Wide phones must fill the screen instead of adding black bars")
	_check(ProjectSettings.get_setting("input_devices/pointing/emulate_mouse_from_touch"), "Touch must drive GUI mouse input")


func _test_board_geometry_and_touch_targets() -> void:
	var view_size := Vector2(940, 508)
	var viewport_rect := Rect2(Vector2.ZERO, view_size)
	for seed in [1, 7, 99, 7777, 8675309, 2147483000]:
		var game := MahjongCore.new()
		game.build_board(seed)
		var layout := BoardLayout.new()
		for pair in game.solution:
			layout.rebuild(view_size, game.tiles)
			_check(layout.tile_size.x >= 60.0 and layout.tile_size.y >= 80.0, "Seed %d: tiles must remain finger-sized" % seed)
			_check(layout.step.x >= layout.tile_size.x * 0.88, "Seed %d: horizontal overlap must stay shallow" % seed)
			_check(layout.step.y >= layout.tile_size.y * 0.78, "Seed %d: vertical overlap must stay shallow" % seed)
			for tile_id in [pair.x, pair.y]:
				var rect: Rect2 = layout.rects.get(tile_id, Rect2())
				_check(rect.has_area() and viewport_rect.encloses(rect), "Seed %d: active tile %d must remain visible" % [seed, tile_id])
				_check(layout.pick_tile(rect.get_center(), game) == tile_id, "Seed %d: centre tap must select tile %d" % [seed, tile_id])
			_check(game.try_remove(pair.x, pair.y), "Seed %d: touch-tested solution pair must remove" % seed)
		_check(game.remaining == 0, "Seed %d: touch-tested board must be completely playable" % seed)
