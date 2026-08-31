class_name LanSession
extends Node

signal rival_connection_changed(connected: bool)
signal match_started(seed: int, countdown_ms: int)
signal rival_progress_changed(remaining: int)
signal match_finished(local_won: bool)
signal session_error(message: String)

const PORT := 7777
const ALPHABET := "0123456789ABCDEFGHJKMNPQRSTVWXYZ"

var local_ip := ""
var room_code := ""
var is_host := false
var rival_connected := false
var match_active := false
var _peer: ENetMultiplayerPeer


func _ready() -> void:
	multiplayer.peer_connected.connect(_on_peer_connected)
	multiplayer.peer_disconnected.connect(_on_peer_disconnected)
	multiplayer.connected_to_server.connect(_on_connected_to_server)
	multiplayer.connection_failed.connect(_on_connection_failed)
	multiplayer.server_disconnected.connect(_on_server_disconnected)


func host_room() -> bool:
	close_session()
	local_ip = _find_lan_ipv4()
	if local_ip.is_empty():
		session_error.emit("Não encontrei um endereço Wi-Fi local.")
		return false
	_peer = ENetMultiplayerPeer.new()
	var error := _peer.create_server(PORT, 1)
	if error != OK:
		session_error.emit("Não foi possível abrir a sala na porta %d." % PORT)
		return false
	multiplayer.multiplayer_peer = _peer
	is_host = true
	room_code = encode_room_code(local_ip)
	return true


func join_room(code_or_ip: String) -> bool:
	close_session()
	var normalized := code_or_ip.strip_edges().to_upper()
	var address := normalized if normalized.contains(".") else decode_room_code(normalized)
	if address.is_empty():
		session_error.emit("Código inválido. Use XXXX-XXXX ou o IP mostrado pelo anfitrião.")
		return false
	_peer = ENetMultiplayerPeer.new()
	var error := _peer.create_client(address, PORT)
	if error != OK:
		session_error.emit("Não consegui iniciar a conexão com %s." % address)
		return false
	multiplayer.multiplayer_peer = _peer
	is_host = false
	local_ip = address
	room_code = normalized if not normalized.contains(".") else encode_room_code(address)
	return true


func start_match(seed: int) -> void:
	if not is_host or not rival_connected:
		session_error.emit("O segundo jogador ainda não entrou.")
		return
	match_active = true
	var countdown_duration_ms := 3200
	_receive_start(seed, countdown_duration_ms)
	_receive_start.rpc(seed, countdown_duration_ms)


func report_progress(remaining: int) -> void:
	if not match_active:
		return
	if is_host:
		_receive_rival_progress.rpc(remaining)
		if remaining == 0:
			_resolve_winner(true)
	else:
		_submit_client_progress.rpc_id(1, remaining)


func close_session() -> void:
	match_active = false
	rival_connected = false
	is_host = false
	room_code = ""
	if multiplayer.multiplayer_peer != null:
		multiplayer.multiplayer_peer.close()
		multiplayer.multiplayer_peer = OfflineMultiplayerPeer.new()
	_peer = null


func encode_room_code(ipv4: String) -> String:
	var parts := ipv4.split(".")
	if parts.size() != 4:
		return ""
	var value: int = 0
	for part in parts:
		var octet := int(part)
		if octet < 0 or octet > 255:
			return ""
		value = (value << 8) | octet
	var body := ""
	for shift in range(30, -1, -5):
		body += ALPHABET[(value >> shift) & 31]
	var checksum := ALPHABET[_checksum(body)]
	var raw := body + checksum
	return raw.substr(0, 4) + "-" + raw.substr(4, 4)


func decode_room_code(code: String) -> String:
	var raw := code.replace("-", "").replace("O", "0").replace("I", "1").replace("L", "1")
	if raw.length() != 8:
		return ""
	var body := raw.substr(0, 7)
	if ALPHABET.find(raw[7]) != _checksum(body):
		return ""
	var value: int = 0
	for character in body:
		var index := ALPHABET.find(character)
		if index < 0:
			return ""
		value = (value << 5) | index
	value &= 0xffffffff
	return "%d.%d.%d.%d" % [(value >> 24) & 255, (value >> 16) & 255, (value >> 8) & 255, value & 255]


func _checksum(body: String) -> int:
	var result := 7
	for character in body:
		result = (result * 17 + ALPHABET.find(character)) % 32
	return result


func _find_lan_ipv4() -> String:
	for address in IP.get_local_addresses():
		var parts := address.split(".")
		if parts.size() != 4:
			continue
		var first := int(parts[0])
		var second := int(parts[1])
		if first == 10 or (first == 192 and second == 168) or (first == 172 and second >= 16 and second <= 31):
			return address
	return ""


func _on_peer_connected(_id: int) -> void:
	if is_host:
		rival_connected = true
		rival_connection_changed.emit(true)


func _on_peer_disconnected(_id: int) -> void:
	rival_connected = false
	match_active = false
	rival_connection_changed.emit(false)
	if is_host:
		session_error.emit("O segundo jogador saiu da sala.")


func _on_connected_to_server() -> void:
	rival_connected = true
	rival_connection_changed.emit(true)


func _on_connection_failed() -> void:
	session_error.emit("A conexão falhou. Confirme o Wi-Fi e o código da sala.")
	close_session()


func _on_server_disconnected() -> void:
	session_error.emit("A sala foi encerrada pelo anfitrião.")
	close_session()


@rpc("authority", "call_remote", "reliable")
func _receive_start(p_seed: int, countdown_ms: int) -> void:
	match_active = true
	match_started.emit(p_seed, countdown_ms)


@rpc("any_peer", "call_remote", "reliable")
func _submit_client_progress(value: int) -> void:
	if not is_host or multiplayer.get_remote_sender_id() == 1:
		return
	rival_progress_changed.emit(value)
	if value == 0:
		_resolve_winner(false)


@rpc("authority", "call_remote", "reliable")
func _receive_rival_progress(value: int) -> void:
	rival_progress_changed.emit(value)


func _resolve_winner(host_won: bool) -> void:
	if not match_active:
		return
	match_active = false
	match_finished.emit(host_won if is_host else not host_won)
	_receive_result.rpc(host_won)


@rpc("authority", "call_remote", "reliable")
func _receive_result(host_won: bool) -> void:
	if is_host:
		return
	match_active = false
	match_finished.emit(not host_won)
