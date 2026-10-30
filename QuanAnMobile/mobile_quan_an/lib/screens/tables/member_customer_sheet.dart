import 'package:flutter/material.dart';

import '../../models/member_customer.dart';
import '../../services/api_client.dart';
import '../../services/app_services.dart';

class MemberCustomerSheet extends StatefulWidget {
  final MemberCustomer? currentMember;

  const MemberCustomerSheet({
    super.key,
    this.currentMember,
  });

  @override
  State<MemberCustomerSheet> createState() => _MemberCustomerSheetState();
}

class _MemberCustomerSheetState extends State<MemberCustomerSheet> {
  late final TextEditingController _phoneController;

  List<MemberCustomer> _results = const <MemberCustomer>[];
  bool _loading = false;
  bool _searched = false;
  String? _errorMessage;

  @override
  void initState() {
    super.initState();
    _phoneController = TextEditingController();
  }

  @override
  void dispose() {
    _phoneController.dispose();
    super.dispose();
  }

  Future<void> _search() async {
    if (_loading) return;

    final String phone = _phoneController.text.trim();
    if (phone.isEmpty) {
      setState(() {
        _searched = false;
        _results = const <MemberCustomer>[];
        _errorMessage = 'Vui lòng nhập số điện thoại cần tìm.';
      });
      return;
    }

    FocusScope.of(context).unfocus();
    setState(() {
      _loading = true;
      _errorMessage = null;
    });

    try {
      final List<MemberCustomer> found =
          await AppServices.customerService.searchByPhone(phone);

      if (!mounted) return;
      setState(() {
        _results = found.where((member) => member.id > 0).toList();
        _searched = true;
        _loading = false;
      });
    } on ApiException catch (error) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _searched = true;
        _results = const <MemberCustomer>[];
        _errorMessage = error.message;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _searched = true;
        _results = const <MemberCustomer>[];
        _errorMessage = 'Không thể tìm thành viên. Vui lòng thử lại.';
      });
    }
  }

  Future<void> _createMember() async {
    if (_loading) return;

    FocusScope.of(context).unfocus();

    final MemberCustomer? created = await showDialog<MemberCustomer>(
      context: context,
      barrierDismissible: false,
      builder: (_) => _CreateMemberDialog(
        initialPhone: _phoneController.text.trim(),
      ),
    );

    if (!mounted || created == null) return;
    Navigator.of(context).pop(created);
  }

  @override
  Widget build(BuildContext context) {
    final MemberCustomer? current = widget.currentMember;

    return SafeArea(
      top: false,
      child: AnimatedPadding(
        duration: const Duration(milliseconds: 160),
        padding: EdgeInsets.fromLTRB(
          20,
          0,
          20,
          20 + MediaQuery.viewInsetsOf(context).bottom,
        ),
        child: SingleChildScrollView(
          keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  const Expanded(
                    child: Text(
                      'Khách hàng / Thành viên',
                      style: TextStyle(
                        fontSize: 20,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                  ),
                  IconButton(
                    onPressed: _loading
                        ? null
                        : () => Navigator.of(context).pop(),
                    icon: const Icon(Icons.close),
                  ),
                ],
              ),
              if (current != null) ...[
                Container(
                  width: double.infinity,
                  padding: const EdgeInsets.all(12),
                  decoration: BoxDecoration(
                    color: Colors.green.withValues(alpha: 0.08),
                    borderRadius: BorderRadius.circular(12),
                  ),
                  child: Text(
                    'Đang gắn: ${current.fullName} • ${current.phone} • ${current.memberLevel}',
                    style: const TextStyle(fontWeight: FontWeight.w600),
                  ),
                ),
                const SizedBox(height: 12),
              ],
              TextField(
                controller: _phoneController,
                enabled: !_loading,
                keyboardType: TextInputType.phone,
                textInputAction: TextInputAction.search,
                decoration: InputDecoration(
                  labelText: 'Số điện thoại khách',
                  hintText: 'Nhập SĐT để tìm thành viên',
                  border: const OutlineInputBorder(),
                  prefixIcon: const Icon(Icons.phone_outlined),
                  suffixIcon: _phoneController.text.isEmpty
                      ? null
                      : IconButton(
                          onPressed: _loading
                              ? null
                              : () {
                                  _phoneController.clear();
                                  setState(() {
                                    _results = const <MemberCustomer>[];
                                    _searched = false;
                                    _errorMessage = null;
                                  });
                                },
                          icon: const Icon(Icons.clear),
                        ),
                ),
                onChanged: (_) {
                  if (mounted) setState(() {});
                },
                onSubmitted: (_) => _search(),
              ),
              const SizedBox(height: 10),
              SizedBox(
                width: double.infinity,
                child: FilledButton.icon(
                  onPressed: _loading ? null : _search,
                  icon: _loading
                      ? const SizedBox(
                          width: 18,
                          height: 18,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Icon(Icons.search),
                  label: Text(_loading ? 'Đang tìm...' : 'Tìm thành viên'),
                ),
              ),
              if (_errorMessage != null) ...[
                const SizedBox(height: 10),
                Text(
                  _errorMessage!,
                  style: TextStyle(
                    color: Theme.of(context).colorScheme.error,
                    fontWeight: FontWeight.w500,
                  ),
                ),
              ],
              if (_searched &&
                  _errorMessage == null &&
                  _results.isEmpty) ...[
                const SizedBox(height: 12),
                const Text(
                  'Không tìm thấy thành viên phù hợp.',
                  style: TextStyle(color: Colors.grey),
                ),
              ],
              if (_results.isNotEmpty) ...[
                const SizedBox(height: 12),
                ConstrainedBox(
                  constraints: const BoxConstraints(maxHeight: 240),
                  child: ListView.separated(
                    shrinkWrap: true,
                    itemCount: _results.length,
                    separatorBuilder: (_, __) => const Divider(height: 1),
                    itemBuilder: (_, int index) {
                      final MemberCustomer member = _results[index];
                      return ListTile(
                        contentPadding: EdgeInsets.zero,
                        leading: const CircleAvatar(
                          child: Icon(Icons.person_outline),
                        ),
                        title: Text(
                          member.fullName.trim().isEmpty
                              ? 'Khách hàng #${member.id}'
                              : member.fullName,
                        ),
                        subtitle: Text(
                          '${member.phone} • ${member.memberLevel} • ${member.loyaltyPoint} điểm',
                        ),
                        trailing: const Icon(Icons.chevron_right),
                        onTap: _loading
                            ? null
                            : () => Navigator.of(context).pop(member),
                      );
                    },
                  ),
                ),
              ],
              const SizedBox(height: 12),
              SizedBox(
                width: double.infinity,
                child: OutlinedButton.icon(
                  onPressed: _loading ? null : _createMember,
                  icon: const Icon(Icons.person_add_alt_1_outlined),
                  label: const Text('Tạo thành viên mới'),
                ),
              ),
              const SizedBox(height: 4),
            ],
          ),
        ),
      ),
    );
  }
}

class _CreateMemberDialog extends StatefulWidget {
  final String initialPhone;

  const _CreateMemberDialog({required this.initialPhone});

  @override
  State<_CreateMemberDialog> createState() => _CreateMemberDialogState();
}

class _CreateMemberDialogState extends State<_CreateMemberDialog> {
  late final TextEditingController _nameController;
  late final TextEditingController _phoneController;

  bool _saving = false;
  String? _errorMessage;

  @override
  void initState() {
    super.initState();
    _nameController = TextEditingController();
    _phoneController = TextEditingController(text: widget.initialPhone);
  }

  @override
  void dispose() {
    _nameController.dispose();
    _phoneController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (_saving) return;

    final String name = _nameController.text.trim();
    final String phone = _phoneController.text.trim();

    if (name.isEmpty || phone.isEmpty) {
      setState(() => _errorMessage = 'Vui lòng nhập tên và số điện thoại.');
      return;
    }

    setState(() {
      _saving = true;
      _errorMessage = null;
    });

    try {
      final MemberCustomer created = await AppServices.customerService.create(
        fullName: name,
        phone: phone,
      );

      if (!mounted) return;

      if (created.id <= 0) {
        setState(() {
          _saving = false;
          _errorMessage = 'Không lấy được thông tin thành viên vừa tạo.';
        });
        return;
      }

      Navigator.of(context).pop(created);
    } on ApiException catch (error) {
      if (!mounted) return;
      setState(() {
        _saving = false;
        _errorMessage = error.message;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _saving = false;
        _errorMessage = 'Không thể tạo thành viên. Vui lòng thử lại.';
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: const Text('Tạo thành viên mới'),
      content: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            TextField(
              controller: _nameController,
              enabled: !_saving,
              textCapitalization: TextCapitalization.words,
              decoration: const InputDecoration(
                labelText: 'Tên khách hàng',
                border: OutlineInputBorder(),
              ),
            ),
            const SizedBox(height: 12),
            TextField(
              controller: _phoneController,
              enabled: !_saving,
              keyboardType: TextInputType.phone,
              textInputAction: TextInputAction.done,
              decoration: const InputDecoration(
                labelText: 'Số điện thoại',
                border: OutlineInputBorder(),
              ),
              onSubmitted: (_) => _submit(),
            ),
            if (_errorMessage != null) ...[
              const SizedBox(height: 12),
              Text(
                _errorMessage!,
                style: TextStyle(
                  color: Theme.of(context).colorScheme.error,
                ),
              ),
            ],
          ],
        ),
      ),
      actions: [
        TextButton(
          onPressed: _saving ? null : () => Navigator.of(context).pop(),
          child: const Text('Hủy'),
        ),
        FilledButton(
          onPressed: _saving ? null : _submit,
          child: _saving
              ? const SizedBox(
                  width: 18,
                  height: 18,
                  child: CircularProgressIndicator(strokeWidth: 2),
                )
              : const Text('Tạo thành viên'),
        ),
      ],
    );
  }
}
