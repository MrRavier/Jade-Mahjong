extends Control

enum ScreenState { MENU, LOBBY, COUNTDOWN, PLAYING, FINISHED }

const C_INK := Color("071e1a")
const C_DEEP := Color("0b332d")
const C_JADE := Color("176b59")
const C_JADE_LIGHT := Color("3fa887")
const C_GOLD := Color("d6aa43")
const C_GOLD_LIGHT := Color("ffe39a")
const C_IVORY := Color("f4e9c8")
const C_RED := Color("a93b2c")

var state := ScreenState.MENU
var core := MahjongCore.new()
var art := TileArt.new()
var lan: LanSession
var audio: JadeAudio
var theme_font: Font

var background: TextureRect
var page_host: Control
var board_view: BoardView
var rival_label: Label
var timer_label: Label
var remaining_label: Label
var speech_label: Label
var hint_button: Button
var redeal_button: Button
var lobby_status: Label
var lobby_code: Label
var start_match_button: Button
var countdown_overlay: PanelContainer
var countdown_label: Label
var result_overlay: PanelContainer
var result_title: Label
var result_body: Label

var selected_tile := -1
var hints_left := 3
var rival_remaining := 144
var match_started_ms := 0
var countdown_end_ms := 0
var penalty_seconds := 0
var solo_mode := false
var match_seed := 1
var _last_timer_second := -1


func _ready() -> void:
	set_process(true)
	set_process_unhandled_input(true)
	theme_font = load("res://fonts/PressStart2P-Regular.ttf")
	theme = _make_theme()
	_build_background()
	lan = LanSession.new()
	lan.name = "LanSession"
	add_child(lan)
	audio = JadeAudio.new()
	audio.name = "JadeAudio"
	add_child(audio)
	lan.rival_connection_changed.connect(_on_rival_connection)
	lan.match_started.connect(_on_network_match_started)
	lan.rival_progress_changed.connect(_on_rival_progress)
	lan.match_finished.connect(_finish_match)
	lan.session_error.connect(_show_message)
	_show_menu()
	if "--capture" in OS.get_cmdline_user_args():
		_start_solo()
		_capture_after_frames.call_deferred()


func _process(_delta: float) -> void:
	if state == ScreenState.COUNTDOWN:
		var left := countdown_end_ms - Time.get_ticks_msec()
		if left <= 0:
			state = ScreenState.PLAYING
			countdown_overlay.visible = false
			match_started_ms = Time.get_ticks_msec()
			audio.play_gong()
			_set_speech("Que o mais veloz honre o Palácio Celestial!")
		else:
			countdown_label.text = str(maxi(1, ceili(float(left) / 1000.0)))
	if state == ScreenState.PLAYING:
		var seconds := int((Time.get_ticks_msec() - match_started_ms) / 1000) + penalty_seconds
		if seconds != _last_timer_second:
			_last_timer_second = seconds
			timer_label.text = _format_time(seconds)
	if board_view != null and board_view.hint_until_ms > 0 and Time.get_ticks_msec() >= board_view.hint_until_ms:
		board_view.clear_hint()


func _unhandled_input(event: InputEvent) -> void:
	if event.is_action_pressed("ui_cancel"):
		if state == ScreenState.MENU:
			get_tree().quit()
		else:
			_back_to_menu()


func _build_background() -> void:
	background = TextureRect.new()
	background.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	background.texture = load("res://art/jade_palace.png")
	background.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	background.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
	background.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(background)
	var tint := ColorRect.new()
	tint.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	tint.color = Color(0.01, 0.07, 0.06, 0.34)
	tint.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(tint)
	page_host = Control.new()
	page_host.name = "Pages"
	page_host.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(page_host)


func _clear_page() -> void:
	for child in page_host.get_children():
		child.queue_free()
	board_view = null
	countdown_overlay = null
	result_overlay = null


func _show_menu() -> void:
	state = ScreenState.MENU
	selected_tile = -1
	_clear_page()
	var safe := _safe_margin(28)
	page_host.add_child(safe)
	var center := CenterContainer.new()
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	safe.add_child(center)
	var panel := _panel(Color(C_INK, 0.95), C_GOLD, 6)
	panel.custom_minimum_size = Vector2(1050, 590)
	center.add_child(panel)
	var shell := MarginContainer.new()
	shell.add_theme_constant_override("margin_left", 26)
	shell.add_theme_constant_override("margin_right", 26)
	shell.add_theme_constant_override("margin_top", 22)
	shell.add_theme_constant_override("margin_bottom", 22)
	panel.add_child(shell)
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 28)
	shell.add_child(row)
	var mascot_panel := _build_mascot_panel(0, "O IMPERADOR DE JADE", "Duas mãos. Um tabuleiro.\nSomente um levará a glória.")
	mascot_panel.custom_minimum_size = Vector2(390, 530)
	row.add_child(mascot_panel)
	var controls := VBoxContainer.new()
	controls.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	controls.alignment = BoxContainer.ALIGNMENT_CENTER
	controls.add_theme_constant_override("separation", 17)
	row.add_child(controls)
	var crest := _label("SHANGHAI", 16, C_GOLD_LIGHT, HORIZONTAL_ALIGNMENT_CENTER)
	controls.add_child(crest)
	var title := _label("JADE MAHJONG", 34, C_IVORY, HORIZONTAL_ALIGNMENT_CENTER)
	controls.add_child(title)
	controls.add_child(_rule())
	controls.add_child(_label("DUELO NO MESMO WI-FI", 14, C_JADE_LIGHT, HORIZONTAL_ALIGNMENT_CENTER))
	controls.add_child(_button("CRIAR SALA", _create_room, C_RED))
	var join_row := HBoxContainer.new()
	join_row.add_theme_constant_override("separation", 10)
	controls.add_child(join_row)
	var room_input := LineEdit.new()
	room_input.name = "RoomInput"
	room_input.placeholder_text = "CÓDIGO OU IP"
	room_input.max_length = 15
	room_input.custom_minimum_size = Vector2(310, 62)
	room_input.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	join_row.add_child(room_input)
	var enter := _button("ENTRAR", func(): _join_room(room_input.text), C_JADE)
	enter.custom_minimum_size.x = 170
	join_row.add_child(enter)
	room_input.text_submitted.connect(func(_text: String): _join_room(room_input.text))
	controls.add_child(_button("TREINO SOLO", _start_solo, Color("214a58")))
	controls.add_child(_label("144 peças • sem servidor • sem mensalidade", 10, Color("b6c9b7"), HORIZONTAL_ALIGNMENT_CENTER))
	var music_button: Button
	music_button = _button("MÚSICA: LIGADA", func():
		var enabled := audio.toggle_music()
		music_button.text = "MÚSICA: " + ("LIGADA" if enabled else "DESLIGADA")
	, Color("493b55"))
	music_button.custom_minimum_size.y = 48
	controls.add_child(music_button)


func _create_room() -> void:
	if lan.host_room():
		solo_mode = false
		_show_lobby(true)


func _join_room(value: String) -> void:
	if lan.join_room(value):
		solo_mode = false
		_show_lobby(false)


func _show_lobby(hosting: bool) -> void:
	state = ScreenState.LOBBY
	_clear_page()
	var safe := _safe_margin(34)
	page_host.add_child(safe)
	var center := CenterContainer.new()
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	safe.add_child(center)
	var panel := _panel(Color(C_INK, 0.96), C_GOLD, 6)
	panel.custom_minimum_size = Vector2(920, 560)
	center.add_child(panel)
	var margin := MarginContainer.new()
	margin.add_theme_constant_override("margin_left", 38)
	margin.add_theme_constant_override("margin_right", 38)
	margin.add_theme_constant_override("margin_top", 28)
	margin.add_theme_constant_override("margin_bottom", 28)
	panel.add_child(margin)
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 32)
	margin.add_child(row)
	var mascot := _build_mascot_panel(1, "SALÃO CELESTIAL", "O selo aguarda o segundo desafiante.")
	mascot.custom_minimum_size = Vector2(340, 490)
	row.add_child(mascot)
	var info := VBoxContainer.new()
	info.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	info.alignment = BoxContainer.ALIGNMENT_CENTER
	info.add_theme_constant_override("separation", 20)
	row.add_child(info)
	info.add_child(_label("SALA LOCAL", 28, C_IVORY, HORIZONTAL_ALIGNMENT_CENTER))
	info.add_child(_rule())
	lobby_status = _label("AGUARDANDO RIVAL..." if hosting else "CONECTANDO...", 13, C_GOLD_LIGHT, HORIZONTAL_ALIGNMENT_CENTER)
	info.add_child(lobby_status)
	lobby_code = _label(lan.room_code if hosting else "••••-••••", 30, C_JADE_LIGHT, HORIZONTAL_ALIGNMENT_CENTER)
	info.add_child(lobby_code)
	if hosting:
		info.add_child(_label("IP  %s  •  PORTA %d" % [lan.local_ip, LanSession.PORT], 10, Color("b7cabb"), HORIZONTAL_ALIGNMENT_CENTER))
		var copy := _button("COPIAR CÓDIGO", func(): DisplayServer.clipboard_set(lan.room_code), Color("29485a"))
		info.add_child(copy)
	start_match_button = _button("INICIAR DUELO", _host_start_match, C_RED)
	start_match_button.disabled = true
	start_match_button.visible = hosting
	info.add_child(start_match_button)
	info.add_child(_button("VOLTAR", _back_to_menu, Color("493b55")))


func _on_rival_connection(connected: bool) -> void:
	if state != ScreenState.LOBBY:
		return
	lobby_status.text = "RIVAL CONECTADO" if connected else "AGUARDANDO RIVAL..."
	lobby_status.modulate = C_JADE_LIGHT if connected else C_GOLD_LIGHT
	if start_match_button != null:
		start_match_button.disabled = not connected
	if connected and not lan.is_host:
		lobby_code.text = "SALA ENCONTRADA"


func _host_start_match() -> void:
	match_seed = int(Time.get_unix_time_from_system()) & 0x7fffffff
	lan.start_match(match_seed)


func _on_network_match_started(seed_value: int, countdown_duration_ms: int) -> void:
	_start_game(seed_value, false, countdown_duration_ms)


func _start_solo() -> void:
	solo_mode = true
	match_seed = int(Time.get_unix_time_from_system()) & 0x7fffffff
	_start_game(match_seed, true, 2500)


func _start_game(seed_value: int, is_solo: bool, countdown_duration_ms: int) -> void:
	solo_mode = is_solo
	match_seed = seed_value
	core.build_board(seed_value)
	hints_left = 3
	rival_remaining = 144
	penalty_seconds = 0
	selected_tile = -1
	_last_timer_second = -1
	_build_game_page()
	state = ScreenState.COUNTDOWN
	countdown_end_ms = Time.get_ticks_msec() + countdown_duration_ms
	countdown_overlay.visible = true
	countdown_label.text = str(ceili(float(countdown_duration_ms) / 1000.0))


func _build_game_page() -> void:
	_clear_page()
	var safe := _safe_margin(12)
	page_host.add_child(safe)
	var stack := VBoxContainer.new()
	stack.add_theme_constant_override("separation", 10)
	safe.add_child(stack)
	var header := _panel(Color(C_INK, 0.95), C_GOLD, 4)
	header.custom_minimum_size.y = 74
	stack.add_child(header)
	var header_margin := MarginContainer.new()
	header_margin.add_theme_constant_override("margin_left", 20)
	header_margin.add_theme_constant_override("margin_right", 20)
	header_margin.add_theme_constant_override("margin_top", 10)
	header_margin.add_theme_constant_override("margin_bottom", 10)
	header.add_child(header_margin)
	var hud := HBoxContainer.new()
	hud.add_theme_constant_override("separation", 24)
	header_margin.add_child(hud)
	hud.add_child(_label("JADE MAHJONG", 18, C_IVORY))
	hud.add_child(_vertical_rule())
	timer_label = _label("00:00", 18, C_GOLD_LIGHT)
	timer_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	hud.add_child(timer_label)
	remaining_label = _label("VOCÊ 144", 13, C_JADE_LIGHT)
	hud.add_child(remaining_label)
	rival_label = _label("TREINO" if solo_mode else "RIVAL 144", 13, C_RED)
	hud.add_child(rival_label)
	var body := HBoxContainer.new()
	body.size_flags_vertical = Control.SIZE_EXPAND_FILL
	body.add_theme_constant_override("separation", 10)
	stack.add_child(body)
	var board_shell := _panel(Color("102923e8"), C_JADE, 5)
	board_shell.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	board_shell.size_flags_stretch_ratio = 4.0
	board_shell.size_flags_vertical = Control.SIZE_EXPAND_FILL
	body.add_child(board_shell)
	var board_margin := MarginContainer.new()
	board_margin.add_theme_constant_override("margin_left", 12)
	board_margin.add_theme_constant_override("margin_right", 12)
	board_margin.add_theme_constant_override("margin_top", 12)
	board_margin.add_theme_constant_override("margin_bottom", 12)
	board_shell.add_child(board_margin)
	board_view = BoardView.new()
	board_view.core = core
	board_view.art = art
	board_view.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	board_view.size_flags_vertical = Control.SIZE_EXPAND_FILL
	board_view.tile_pressed.connect(_on_tile_pressed)
	board_margin.add_child(board_view)
	var rail := _build_game_rail()
	rail.custom_minimum_size.x = 270
	rail.size_flags_stretch_ratio = 1.0
	body.add_child(rail)
	var footer := _panel(Color(C_INK, 0.96), C_GOLD, 4)
	footer.custom_minimum_size.y = 70
	stack.add_child(footer)
	var footer_margin := MarginContainer.new()
	footer_margin.add_theme_constant_override("margin_left", 12)
	footer_margin.add_theme_constant_override("margin_right", 12)
	footer_margin.add_theme_constant_override("margin_top", 8)
	footer_margin.add_theme_constant_override("margin_bottom", 8)
	footer.add_child(footer_margin)
	var actions := HBoxContainer.new()
	actions.add_theme_constant_override("separation", 10)
	footer_margin.add_child(actions)
	hint_button = _button("DICA ×3", _use_hint, Color("29485a"))
	hint_button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	actions.add_child(hint_button)
	redeal_button = _button("REORGANIZAR", _redeal, C_JADE)
	redeal_button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	actions.add_child(redeal_button)
	var exit_button := _button("SAIR", _back_to_menu, Color("493b55"))
	exit_button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	actions.add_child(exit_button)
	_build_countdown_overlay()
	_build_result_overlay()


func _build_game_rail() -> PanelContainer:
	var panel := _panel(Color("061b18f2"), C_GOLD, 5)
	var margin := MarginContainer.new()
	margin.add_theme_constant_override("margin_left", 12)
	margin.add_theme_constant_override("margin_right", 12)
	margin.add_theme_constant_override("margin_top", 12)
	margin.add_theme_constant_override("margin_bottom", 12)
	panel.add_child(margin)
	var stack := VBoxContainer.new()
	stack.add_theme_constant_override("separation", 8)
	margin.add_child(stack)
	stack.add_child(_label("ÁRBITRO CELESTIAL", 10, C_GOLD_LIGHT, HORIZONTAL_ALIGNMENT_CENTER))
	var portrait := _emperor_portrait(2)
	portrait.custom_minimum_size = Vector2(230, 270)
	portrait.size_flags_vertical = Control.SIZE_EXPAND_FILL
	stack.add_child(portrait)
	var bubble := _panel(Color("f0e5c5f5"), C_RED, 4)
	bubble.custom_minimum_size.y = 132
	stack.add_child(bubble)
	var speech_margin := MarginContainer.new()
	speech_margin.add_theme_constant_override("margin_left", 12)
	speech_margin.add_theme_constant_override("margin_right", 12)
	speech_margin.add_theme_constant_override("margin_top", 12)
	speech_margin.add_theme_constant_override("margin_bottom", 12)
	bubble.add_child(speech_margin)
	speech_label = _label("Escolha duas peças livres e iguais.", 10, C_INK, HORIZONTAL_ALIGNMENT_CENTER)
	speech_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	speech_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	speech_margin.add_child(speech_label)
	return panel


func _build_countdown_overlay() -> void:
	countdown_overlay = _panel(Color("061b18ee"), C_GOLD_LIGHT, 7)
	countdown_overlay.custom_minimum_size = Vector2(320, 250)
	countdown_overlay.position = Vector2.ZERO
	countdown_overlay.set_anchors_preset(Control.PRESET_CENTER)
	countdown_overlay.grow_horizontal = Control.GROW_DIRECTION_BOTH
	countdown_overlay.grow_vertical = Control.GROW_DIRECTION_BOTH
	page_host.add_child(countdown_overlay)
	var stack := VBoxContainer.new()
	stack.alignment = BoxContainer.ALIGNMENT_CENTER
	countdown_overlay.add_child(stack)
	stack.add_child(_label("O GONGO SOARÁ EM", 12, C_IVORY, HORIZONTAL_ALIGNMENT_CENTER))
	countdown_label = _label("3", 72, C_GOLD_LIGHT, HORIZONTAL_ALIGNMENT_CENTER)
	stack.add_child(countdown_label)


func _build_result_overlay() -> void:
	result_overlay = _panel(Color("061b18f7"), C_GOLD, 7)
	result_overlay.custom_minimum_size = Vector2(610, 390)
	result_overlay.set_anchors_preset(Control.PRESET_CENTER)
	result_overlay.grow_horizontal = Control.GROW_DIRECTION_BOTH
	result_overlay.grow_vertical = Control.GROW_DIRECTION_BOTH
	result_overlay.visible = false
	page_host.add_child(result_overlay)
	var margin := MarginContainer.new()
	margin.add_theme_constant_override("margin_left", 34)
	margin.add_theme_constant_override("margin_right", 34)
	margin.add_theme_constant_override("margin_top", 30)
	margin.add_theme_constant_override("margin_bottom", 30)
	result_overlay.add_child(margin)
	var stack := VBoxContainer.new()
	stack.alignment = BoxContainer.ALIGNMENT_CENTER
	stack.add_theme_constant_override("separation", 22)
	margin.add_child(stack)
	result_title = _label("VITÓRIA CELESTIAL", 26, C_GOLD_LIGHT, HORIZONTAL_ALIGNMENT_CENTER)
	stack.add_child(result_title)
	result_body = _label("", 12, C_IVORY, HORIZONTAL_ALIGNMENT_CENTER)
	result_body.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	stack.add_child(result_body)
	stack.add_child(_button("NOVO DUELO", _back_to_menu, C_RED))


func _on_tile_pressed(tile_id: int) -> void:
	if state != ScreenState.PLAYING:
		return
	if not core.is_free(tile_id):
		audio.play_error()
		_set_speech("Essa peça está presa. Liberte o topo ou um dos lados.")
		board_view.flash_locked(tile_id)
		return
	if selected_tile < 0:
		selected_tile = tile_id
		board_view.selected_id = tile_id
		board_view.queue_redraw()
		_set_speech("Boa escolha. Agora encontre o par correspondente.")
		return
	if selected_tile == tile_id:
		selected_tile = -1
		board_view.selected_id = -1
		board_view.queue_redraw()
		return
	if core.try_remove(selected_tile, tile_id):
		audio.play_pair()
		selected_tile = -1
		board_view.selected_id = -1
		board_view.queue_redraw()
		remaining_label.text = "VOCÊ %03d" % core.remaining
		if not solo_mode:
			lan.report_progress(core.remaining)
		_set_speech(_progress_speech(core.remaining))
		if core.remaining == 0:
			if solo_mode:
				_finish_match(true)
		elif not core.has_available_pair():
			_set_speech("O destino fechou o caminho. Use REORGANIZAR.")
	else:
		audio.play_error()
		board_view.shake_until_ms = Time.get_ticks_msec() + 260
		selected_tile = -1
		board_view.selected_id = -1
		board_view.queue_redraw()
		_set_speech("Essas peças não formam um par.")


func _use_hint() -> void:
	if state != ScreenState.PLAYING or hints_left <= 0:
		return
	var pair := core.available_pair()
	if pair.x < 0:
		_set_speech("Não existe par livre. Reorganize o tabuleiro.")
		return
	hints_left -= 1
	hint_button.text = "DICA ×%d" % hints_left
	hint_button.disabled = hints_left == 0
	board_view.show_hint(pair.x, pair.y)
	_set_speech("Os selos dourados revelam um caminho possível.")


func _redeal() -> void:
	if state != ScreenState.PLAYING:
		return
	if core.has_available_pair():
		audio.play_error()
		_set_speech("Ainda existe uma jogada. Observe as bordas com atenção.")
		return
	if core.redeal_remaining():
		penalty_seconds += 15
		board_view.queue_redraw()
		audio.play_gong()
		_set_speech("O palácio mudou: quinze segundos foram somados.")
	else:
		_show_message("Não foi possível reorganizar as peças restantes.")


func _on_rival_progress(value: int) -> void:
	rival_remaining = value
	if rival_label != null:
		rival_label.text = "RIVAL %03d" % value


func _finish_match(local_won: bool) -> void:
	if state == ScreenState.FINISHED:
		return
	state = ScreenState.FINISHED
	audio.play_victory() if local_won else audio.play_gong()
	result_overlay.visible = true
	result_title.text = "VITÓRIA CELESTIAL" if local_won else "O RIVAL VENCEU"
	result_title.modulate = C_GOLD_LIGHT if local_won else Color("e07b67")
	var elapsed := int((Time.get_ticks_msec() - match_started_ms) / 1000) + penalty_seconds
	result_body.text = ("O Imperador de Jade reconhece sua velocidade.\nTempo: %s" if local_won else "A corte silencia. Treine e desafie novamente.\nTempo: %s") % _format_time(maxi(0, elapsed))
	_set_speech("Seu nome ecoará pelos Nove Céus!" if local_won else "Até o jade mais bruto pode ser lapidado.")


func _back_to_menu() -> void:
	lan.close_session()
	_show_menu()


func _show_message(message: String) -> void:
	var dialog := AcceptDialog.new()
	dialog.title = "MENSAGEM DO PALÁCIO"
	dialog.dialog_text = message
	page_host.add_child(dialog)
	dialog.popup_centered(Vector2i(700, 260))


func _set_speech(value: String) -> void:
	if speech_label != null:
		speech_label.text = value


func _progress_speech(value: int) -> String:
	if value <= 24:
		return "O fim se aproxima. Não desvie os olhos do jade!"
	if value <= 72:
		return "Sua leitura do tabuleiro agrada à corte."
	if value <= 120:
		return "As primeiras camadas cedem ao seu toque."
	return "Um par digno. Continue."


func _build_mascot_panel(pose: int, heading: String, quote: String) -> PanelContainer:
	var panel := _panel(Color("09251ff2"), C_JADE, 4)
	var margin := MarginContainer.new()
	margin.add_theme_constant_override("margin_left", 10)
	margin.add_theme_constant_override("margin_right", 10)
	margin.add_theme_constant_override("margin_top", 10)
	margin.add_theme_constant_override("margin_bottom", 10)
	panel.add_child(margin)
	var stack := VBoxContainer.new()
	stack.add_theme_constant_override("separation", 8)
	margin.add_child(stack)
	stack.add_child(_label(heading, 11, C_GOLD_LIGHT, HORIZONTAL_ALIGNMENT_CENTER))
	var portrait := _emperor_portrait(pose)
	portrait.size_flags_vertical = Control.SIZE_EXPAND_FILL
	stack.add_child(portrait)
	var text := _label(quote, 10, C_IVORY, HORIZONTAL_ALIGNMENT_CENTER)
	text.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	stack.add_child(text)
	return panel


func _emperor_portrait(pose: int) -> TextureRect:
	var atlas := AtlasTexture.new()
	atlas.atlas = load("res://art/jade_emperor_sheet.png")
	var column := pose % 3
	var row := (pose / 3) as int
	atlas.region = Rect2(column * 512, row * 512, 512, 512)
	var portrait := TextureRect.new()
	portrait.texture = atlas
	portrait.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	portrait.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	portrait.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	portrait.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return portrait


func _safe_margin(amount: int) -> MarginContainer:
	var safe := MarginContainer.new()
	safe.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	for side in ["left", "right", "top", "bottom"]:
		safe.add_theme_constant_override("margin_" + side, amount)
	return safe


func _panel(fill: Color, border: Color, width: int) -> PanelContainer:
	var panel := PanelContainer.new()
	var style := StyleBoxFlat.new()
	style.bg_color = fill
	style.border_color = border
	style.set_border_width_all(width)
	style.shadow_color = Color(0, 0, 0, 0.65)
	style.shadow_size = 8
	style.shadow_offset = Vector2(6, 6)
	style.corner_radius_top_left = 2
	style.corner_radius_top_right = 2
	style.corner_radius_bottom_left = 2
	style.corner_radius_bottom_right = 2
	panel.add_theme_stylebox_override("panel", style)
	return panel


func _button(text_value: String, callback: Callable, color: Color) -> Button:
	var button := Button.new()
	button.text = text_value
	button.custom_minimum_size = Vector2(220, 62)
	button.add_theme_font_size_override("font_size", 13)
	button.add_theme_color_override("font_color", C_IVORY)
	button.add_theme_color_override("font_hover_color", C_GOLD_LIGHT)
	button.add_theme_color_override("font_pressed_color", C_INK)
	button.add_theme_stylebox_override("normal", _button_style(color.darkened(0.40), C_GOLD, 4))
	button.add_theme_stylebox_override("hover", _button_style(color.darkened(0.18), C_GOLD_LIGHT, 4))
	button.add_theme_stylebox_override("pressed", _button_style(C_GOLD, C_IVORY, 4))
	button.add_theme_stylebox_override("disabled", _button_style(Color("26332f"), Color("56635c"), 3))
	button.pressed.connect(callback)
	return button


func _button_style(fill: Color, border: Color, width: int) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = fill
	style.border_color = border
	style.set_border_width_all(width)
	style.content_margin_left = 15
	style.content_margin_right = 15
	style.content_margin_top = 12
	style.content_margin_bottom = 12
	style.shadow_color = Color(0, 0, 0, 0.7)
	style.shadow_size = 4
	style.shadow_offset = Vector2(4, 4)
	return style


func _label(value: String, font_size: int, color: Color, alignment := HORIZONTAL_ALIGNMENT_LEFT) -> Label:
	var label := Label.new()
	label.text = value
	label.add_theme_font_size_override("font_size", font_size)
	label.add_theme_color_override("font_color", color)
	label.horizontal_alignment = alignment
	label.size_flags_horizontal = Control.SIZE_EXPAND_FILL if alignment == HORIZONTAL_ALIGNMENT_CENTER else Control.SIZE_SHRINK_BEGIN
	return label


func _rule() -> ColorRect:
	var line := ColorRect.new()
	line.color = C_GOLD
	line.custom_minimum_size.y = 3
	return line


func _vertical_rule() -> ColorRect:
	var line := ColorRect.new()
	line.color = C_GOLD
	line.custom_minimum_size.x = 3
	return line


func _make_theme() -> Theme:
	var result := Theme.new()
	result.default_font = theme_font
	result.default_font_size = 12
	var input_style := _button_style(Color("f2e7c8"), C_GOLD, 4)
	result.set_stylebox("normal", "LineEdit", input_style)
	result.set_stylebox("focus", "LineEdit", _button_style(Color("fff3d2"), C_JADE_LIGHT, 4))
	result.set_color("font_color", "LineEdit", C_INK)
	result.set_color("font_placeholder_color", "LineEdit", Color("6b7167"))
	result.set_font_size("font_size", "LineEdit", 12)
	return result


func _format_time(total_seconds: int) -> String:
	return "%02d:%02d" % [total_seconds / 60, total_seconds % 60]


func _capture_after_frames() -> void:
	await get_tree().process_frame
	await get_tree().process_frame
	await get_tree().create_timer(0.5).timeout
	var image := get_viewport().get_texture().get_image()
	image.save_png("/tmp/jade-mahjong-capture.png")
	get_tree().quit()


class BoardView:
	extends Control

	signal tile_pressed(tile_id: int)

	var core: MahjongCore
	var art: TileArt
	var selected_id := -1
	var hint_ids := Vector2i(-1, -1)
	var hint_until_ms := 0
	var shake_until_ms := 0
	var locked_id := -1
	var locked_until_ms := 0
	var _rects: Dictionary = {}

	func _ready() -> void:
		mouse_filter = Control.MOUSE_FILTER_STOP
		resized.connect(queue_redraw)

	func _process(_delta: float) -> void:
		if Time.get_ticks_msec() < shake_until_ms or Time.get_ticks_msec() < locked_until_ms:
			queue_redraw()

	func _draw() -> void:
		if core == null or art == null:
			return
		var backdrop := Rect2(Vector2(6, 6), size - Vector2(12, 12))
		draw_rect(backdrop, Color("061b18d6"), true)
		draw_rect(backdrop, Color("3fa887"), false, 3.0)
		# Carved palace-table diagonals and corner medallions.
		for x in range(26, int(size.x), 46):
			draw_line(Vector2(x, 12), Vector2(x - 18, size.y - 12), Color("15453c70"), 2.0)
		for corner in [Vector2(24, 24), Vector2(size.x - 24, 24), Vector2(24, size.y - 24), Vector2(size.x - 24, size.y - 24)]:
			draw_circle(corner, 10, Color("d6aa43"))
			draw_circle(corner, 6, Color("176b59"))
		_rects.clear()
		var tile_w := minf((size.x - 70.0) / 9.65, (size.y - 48.0) / 4.36 * 0.75)
		tile_w = clampf(tile_w, 42.0, 96.0)
		var tile_h := tile_w * 4.0 / 3.0
		var step_x := tile_w * 0.76
		var step_y := tile_h * 0.48
		var layout_w := 11.0 * step_x + tile_w + 20.0
		var layout_h := 7.0 * step_y + tile_h + 20.0
		var origin := Vector2((size.x - layout_w) * 0.5, (size.y - layout_h) * 0.5)
		var shake := 0.0
		if Time.get_ticks_msec() < shake_until_ms:
			shake = sin(Time.get_ticks_msec() * 0.08) * 5.0
		var draw_ids: Array[int] = []
		for i in core.tiles.size():
			if core.tiles[i].active:
				draw_ids.append(i)
		draw_ids.sort_custom(func(a: int, b: int) -> bool:
			var ta: Dictionary = core.tiles[a]
			var tb: Dictionary = core.tiles[b]
			if ta.layer != tb.layer:
				return ta.layer < tb.layer
			if ta.gy != tb.gy:
				return ta.gy < tb.gy
			return ta.gx < tb.gx
		)
		for tile_id in draw_ids:
			var tile: Dictionary = core.tiles[tile_id]
			var px: float = origin.x + float(tile.gx) * 0.5 * step_x + float(tile.layer) * 5.0 + shake
			var py: float = origin.y + float(tile.gy) * 0.5 * step_y - float(tile.layer) * 5.0
			var rect := Rect2(px, py, tile_w, tile_h)
			_rects[tile_id] = rect
			var free := core.is_free(tile_id)
			var selected := tile_id == selected_id
			var texture: Texture2D = art.texture_for(tile.kind, free, selected)
			draw_texture_rect(texture, rect, false)
			if tile_id == hint_ids.x or tile_id == hint_ids.y:
				draw_rect(rect.grow(3), Color("ffe39a"), false, 5.0)
				draw_circle(rect.position + Vector2(rect.size.x - 8, 8), 7, Color("ffe39a"))
			if tile_id == locked_id and Time.get_ticks_msec() < locked_until_ms:
				draw_rect(rect, Color("a93b2c77"), true)

	func _gui_input(event: InputEvent) -> void:
		var pressed := false
		var point := Vector2.ZERO
		if event is InputEventScreenTouch and event.pressed:
			pressed = true
			point = event.position
		elif event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT and event.pressed:
			pressed = true
			point = event.position
		if not pressed:
			return
		var candidates: Array[int] = []
		for tile_id in _rects:
			if _rects[tile_id].has_point(point):
				candidates.append(tile_id)
		candidates.sort_custom(func(a: int, b: int) -> bool:
			var ta: Dictionary = core.tiles[a]
			var tb: Dictionary = core.tiles[b]
			return ta.layer > tb.layer or (ta.layer == tb.layer and ta.id > tb.id)
		)
		if not candidates.is_empty():
			tile_pressed.emit(candidates[0])
			accept_event()

	func show_hint(a: int, b: int) -> void:
		hint_ids = Vector2i(a, b)
		hint_until_ms = Time.get_ticks_msec() + 2300
		queue_redraw()

	func clear_hint() -> void:
		hint_ids = Vector2i(-1, -1)
		hint_until_ms = 0
		queue_redraw()

	func flash_locked(tile_id: int) -> void:
		locked_id = tile_id
		locked_until_ms = Time.get_ticks_msec() + 420
		queue_redraw()
