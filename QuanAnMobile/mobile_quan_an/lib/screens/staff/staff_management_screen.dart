import 'package:flutter/material.dart';

import '../../app/theme.dart';
import '../../models/staff_management_models.dart';
import '../../services/api_client.dart';
import '../../services/app_services.dart';
import '../../widgets/top_message.dart';

class StaffManagementScreen extends StatefulWidget {
  const StaffManagementScreen({super.key});

  @override
  State<StaffManagementScreen> createState() => _StaffManagementScreenState();
}

class _StaffManagementScreenState extends State<StaffManagementScreen> {
  List<ManagedEmployee> employees = [];
  List<ManagedShift> shifts = [];
  List<ManagedArea> areas = [];
  List<ManagedShiftAssignment> assignments = [];
  List<ManagedStaffTask> tasks = [];
  List<ManagedShiftAssignment> history = [];

  DateTime selectedDate = DateTime.now();
  int? taskUserId;
  int? historyUserId;
  int historyDays = 7;
  bool loading = true;
  bool loadingAssignments = false;
  bool loadingTasks = false;
  bool loadingHistory = false;
  String? error;

  @override
  void initState() {
    super.initState();
    _loadBase();
  }

  Future<void> _loadBase() async {
    setState(() {
      loading = true;
      error = null;
    });
    try {
      final result = await Future.wait<dynamic>([
        AppServices.staffManagementService.getEmployees(),
        AppServices.staffManagementService.getShifts(),
        AppServices.staffManagementService.getAreas(),
      ]);
      if (!mounted) return;
      setState(() {
        employees = result[0] as List<ManagedEmployee>;
        shifts = result[1] as List<ManagedShift>;
        areas = result[2] as List<ManagedArea>;
        loading = false;
      });
      await Future.wait<void>([
        _loadAssignments(),
        _loadTasks(),
        _loadHistory(),
      ]);
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        loading = false;
        error = e.message;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        loading = false;
        error = 'Không thể tải dữ liệu quản lý nhân viên';
      });
    }
  }

  Future<void> _loadAssignments() async {
    if (mounted) setState(() => loadingAssignments = true);
    try {
      final rows = await AppServices.staffManagementService
          .getAssignments(selectedDate);
      if (!mounted) return;
      setState(() {
        assignments = rows;
        loadingAssignments = false;
      });
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() => loadingAssignments = false);
      _show(e.message, TopMessageType.error);
    }
  }

  Future<void> _loadTasks() async {
    if (mounted) setState(() => loadingTasks = true);
    try {
      final rows = await AppServices.staffManagementService
          .getTasks(userId: taskUserId);
      if (!mounted) return;
      setState(() {
        tasks = rows;
        loadingTasks = false;
      });
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() => loadingTasks = false);
      _show(e.message, TopMessageType.error);
    }
  }

  Future<void> _loadHistory() async {
    if (mounted) setState(() => loadingHistory = true);
    final to = DateTime.now();
    final from = to.subtract(Duration(days: historyDays - 1));
    try {
      final rows = await AppServices.staffManagementService.getHistory(
        userId: historyUserId,
        from: from,
        to: to,
      );
      if (!mounted) return;
      setState(() {
        history = rows;
        loadingHistory = false;
      });
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() => loadingHistory = false);
      _show(e.message, TopMessageType.error);
    }
  }

  void _show(String message, TopMessageType type) {
    if (!mounted) return;
    TopMessage.show(context, message: message, type: type);
  }

  Future<void> _pickDate() async {
    final picked = await showDatePicker(
      context: context,
      initialDate: selectedDate,
      firstDate: DateTime.now().subtract(const Duration(days: 365)),
      lastDate: DateTime.now().add(const Duration(days: 730)),
    );
    if (picked == null || !mounted) return;
    setState(() => selectedDate = picked);
    await _loadAssignments();
  }

  Future<void> _assignShift() async {
    final active = employees.where((e) => e.isActive).toList();
    if (active.isEmpty || shifts.isEmpty) {
      _show(
        'Cần có nhân viên đang hoạt động và ca làm trước khi phân ca.',
        TopMessageType.warning,
      );
      return;
    }

    ManagedEmployee employee = active.first;
    ManagedShift shift = shifts.first;
    ManagedArea? area;

    final saved = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      builder: (sheetContext) => StatefulBuilder(
        builder: (context, setSheetState) => Padding(
          padding: EdgeInsets.fromLTRB(
            18,
            4,
            18,
            MediaQuery.viewInsetsOf(context).bottom + 20,
          ),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Align(
                alignment: Alignment.centerLeft,
                child: Text(
                  'Phân ca nhân viên',
                  style: TextStyle(fontSize: 20, fontWeight: FontWeight.w700),
                ),
              ),
              const SizedBox(height: 16),
              DropdownButtonFormField<ManagedEmployee>(
                value: employee,
                isExpanded: true,
                decoration: const InputDecoration(
                  labelText: 'Nhân viên',
                  border: OutlineInputBorder(),
                ),
                items: active
                    .map(
                      (e) => DropdownMenuItem(
                        value: e,
                        child: Text(e.fullName, overflow: TextOverflow.ellipsis),
                      ),
                    )
                    .toList(),
                onChanged: (v) {
                  if (v != null) setSheetState(() => employee = v);
                },
              ),
              const SizedBox(height: 12),
              DropdownButtonFormField<ManagedShift>(
                value: shift,
                isExpanded: true,
                decoration: const InputDecoration(
                  labelText: 'Ca làm',
                  border: OutlineInputBorder(),
                ),
                items: shifts
                    .map(
                      (s) => DropdownMenuItem(
                        value: s,
                        child: Text('${s.name} · ${s.timeLabel}'),
                      ),
                    )
                    .toList(),
                onChanged: (v) {
                  if (v != null) setSheetState(() => shift = v);
                },
              ),
              const SizedBox(height: 12),
              DropdownButtonFormField<ManagedArea?>(
                value: area,
                isExpanded: true,
                decoration: const InputDecoration(
                  labelText: 'Khu vực phụ trách',
                  border: OutlineInputBorder(),
                ),
                items: [
                  const DropdownMenuItem<ManagedArea?>(
                    value: null,
                    child: Text('Chưa phân khu vực'),
                  ),
                  ...areas.map(
                    (a) => DropdownMenuItem<ManagedArea?>(
                      value: a,
                      child: Text(a.name),
                    ),
                  ),
                ],
                onChanged: (v) => setSheetState(() => area = v),
              ),
              const SizedBox(height: 16),
              SizedBox(
                width: double.infinity,
                height: 48,
                child: FilledButton.icon(
                  onPressed: () async {
                    try {
                      await AppServices.staffManagementService.assignShift(
                        userId: employee.userId,
                        shiftId: shift.id,
                        workDate: selectedDate,
                        areaId: area?.id,
                      );
                      if (sheetContext.mounted) {
                        Navigator.pop(sheetContext, true);
                      }
                    } on ApiException catch (e) {
                      if (sheetContext.mounted) {
                        TopMessage.show(
                          sheetContext,
                          message: e.message,
                          type: TopMessageType.error,
                        );
                      }
                    }
                  },
                  icon: const Icon(Icons.event_available_outlined),
                  label: Text('Phân ca ngày ${_date(selectedDate)}'),
                ),
              ),
            ],
          ),
        ),
      ),
    );

    if (saved == true) {
      await _loadAssignments();
      await _loadHistory();
      _show('Đã phân ca cho nhân viên', TopMessageType.success);
    }
  }

  Future<void> _changeAssignmentStatus(
    ManagedShiftAssignment assignment,
    int status,
  ) async {
    try {
      await AppServices.staffManagementService
          .updateAssignmentStatus(assignment.id, status);
      await _loadAssignments();
      await _loadHistory();
      _show('Đã cập nhật trạng thái ca', TopMessageType.success);
    } on ApiException catch (e) {
      _show(e.message, TopMessageType.error);
    }
  }

  Future<void> _changeTaskStatus(
    ManagedStaffTask task,
    int status,
  ) async {
    try {
      await AppServices.staffManagementService
          .updateTaskStatus(task.id, status);
      await _loadTasks();
      _show('Đã cập nhật trạng thái nhiệm vụ', TopMessageType.success);
    } on ApiException catch (e) {
      _show(e.message, TopMessageType.error);
    }
  }

  Future<void> _deleteAssignment(ManagedShiftAssignment a) async {
    if (!await _confirm(
      'Xóa phân ca',
      'Xóa ca ${a.shiftName} của ${a.userName} ngày ${_date(a.workDate)}?',
    )) {
      return;
    }
    try {
      await AppServices.staffManagementService.deleteAssignment(a.id);
      await _loadAssignments();
      await _loadHistory();
      _show('Đã xóa phân ca', TopMessageType.success);
    } on ApiException catch (e) {
      _show(e.message, TopMessageType.error);
    }
  }

  Future<void> _editTask([ManagedStaffTask? task]) async {
    final active = employees.where((e) => e.isActive).toList();
    if (active.isEmpty) {
      _show('Chưa có nhân viên đang hoạt động.', TopMessageType.warning);
      return;
    }

    final title = TextEditingController(text: task?.title ?? '');
    final description = TextEditingController(text: task?.description ?? '');
    ManagedEmployee employee = active.firstWhere(
      (e) => e.userId == task?.assignedToUserId,
      orElse: () => active.first,
    );
    DateTime? dueAt = task?.dueAt;

    final saved = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      builder: (sheetContext) => StatefulBuilder(
        builder: (context, setSheetState) => Padding(
          padding: EdgeInsets.fromLTRB(
            18,
            4,
            18,
            MediaQuery.viewInsetsOf(context).bottom + 20,
          ),
          child: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Align(
                  alignment: Alignment.centerLeft,
                  child: Text(
                    task == null ? 'Giao nhiệm vụ' : 'Sửa nhiệm vụ',
                    style: const TextStyle(
                      fontSize: 20,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
                const SizedBox(height: 16),
                DropdownButtonFormField<ManagedEmployee>(
                  value: employee,
                  isExpanded: true,
                  decoration: const InputDecoration(
                    labelText: 'Nhân viên',
                    border: OutlineInputBorder(),
                  ),
                  items: active
                      .map(
                        (e) => DropdownMenuItem(
                          value: e,
                          child: Text(
                            e.fullName,
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                      )
                      .toList(),
                  onChanged: (v) {
                    if (v != null) setSheetState(() => employee = v);
                  },
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: title,
                  decoration: const InputDecoration(
                    labelText: 'Tên nhiệm vụ',
                    border: OutlineInputBorder(),
                  ),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: description,
                  minLines: 2,
                  maxLines: 4,
                  decoration: const InputDecoration(
                    labelText: 'Mô tả',
                    border: OutlineInputBorder(),
                  ),
                ),
                const SizedBox(height: 8),
                ListTile(
                  contentPadding: EdgeInsets.zero,
                  leading: const Icon(Icons.calendar_month_outlined),
                  title: const Text('Hạn hoàn thành'),
                  subtitle: Text(
                    dueAt == null ? 'Không đặt hạn' : _dateTime(dueAt!),
                  ),
                  onTap: () async {
                    final picked = await showDatePicker(
                      context: sheetContext,
                      initialDate: dueAt ?? DateTime.now(),
                      firstDate:
                          DateTime.now().subtract(const Duration(days: 365)),
                      lastDate: DateTime.now().add(const Duration(days: 730)),
                    );
                    if (picked != null) {
                      setSheetState(() {
                        dueAt = DateTime(
                          picked.year,
                          picked.month,
                          picked.day,
                          23,
                          59,
                        );
                      });
                    }
                  },
                  trailing: dueAt == null
                      ? null
                      : IconButton(
                          onPressed: () => setSheetState(() => dueAt = null),
                          icon: const Icon(Icons.close),
                        ),
                ),
                const SizedBox(height: 10),
                SizedBox(
                  width: double.infinity,
                  height: 48,
                  child: FilledButton(
                    onPressed: () async {
                      final taskTitle = title.text.trim();
                      if (taskTitle.isEmpty) {
                        TopMessage.show(
                          sheetContext,
                          message: 'Vui lòng nhập tên nhiệm vụ',
                          type: TopMessageType.warning,
                        );
                        return;
                      }
                      try {
                        final desc = description.text.trim();
                        if (task == null) {
                          await AppServices.staffManagementService.createTask(
                            title: taskTitle,
                            description: desc.isEmpty ? null : desc,
                            assignedToUserId: employee.userId,
                            dueAt: dueAt,
                          );
                        } else {
                          await AppServices.staffManagementService.updateTask(
                            id: task.id,
                            title: taskTitle,
                            description: desc.isEmpty ? null : desc,
                            assignedToUserId: employee.userId,
                            dueAt: dueAt,
                          );
                        }
                        if (sheetContext.mounted) {
                          Navigator.pop(sheetContext, true);
                        }
                      } on ApiException catch (e) {
                        if (sheetContext.mounted) {
                          TopMessage.show(
                            sheetContext,
                            message: e.message,
                            type: TopMessageType.error,
                          );
                        }
                      }
                    },
                    child: Text(task == null ? 'Giao nhiệm vụ' : 'Lưu thay đổi'),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );

    title.dispose();
    description.dispose();

    if (saved == true) {
      await _loadTasks();
      _show(
        task == null ? 'Đã giao nhiệm vụ' : 'Đã cập nhật nhiệm vụ',
        TopMessageType.success,
      );
    }
  }

  Future<void> _deleteTask(ManagedStaffTask task) async {
    if (!await _confirm(
      'Xóa nhiệm vụ',
      'Xóa nhiệm vụ “${task.title}” của ${task.assignedToName}?',
    )) {
      return;
    }
    try {
      await AppServices.staffManagementService.deleteTask(task.id);
      await _loadTasks();
      _show('Đã xóa nhiệm vụ', TopMessageType.success);
    } on ApiException catch (e) {
      _show(e.message, TopMessageType.error);
    }
  }

  Future<bool> _confirm(String title, String content) async {
    return await showDialog<bool>(
          context: context,
          builder: (c) => AlertDialog(
            title: Text(title),
            content: Text(content),
            actions: [
              TextButton(
                onPressed: () => Navigator.pop(c, false),
                child: const Text('Hủy'),
              ),
              FilledButton(
                onPressed: () => Navigator.pop(c, true),
                child: const Text('Xác nhận'),
              ),
            ],
          ),
        ) ??
        false;
  }

  @override
  Widget build(BuildContext context) {
    if (loading) {
      return const Scaffold(
        body: Center(
          child: CircularProgressIndicator(color: AppTheme.orangeColor),
        ),
      );
    }
    if (error != null) {
      return Scaffold(
        appBar: AppBar(title: const Text('Quản lý nhân viên')),
        body: Center(
          child: FilledButton.icon(
            onPressed: _loadBase,
            icon: const Icon(Icons.refresh),
            label: Text(error!),
          ),
        ),
      );
    }

    return DefaultTabController(
      length: 3,
      child: Scaffold(
        backgroundColor: const Color(0xFFF6F7F9),
        appBar: AppBar(
          backgroundColor: Colors.white,
          title: const Text(
            'Quản lý nhân viên',
            style: TextStyle(
              color: AppTheme.textColor,
              fontWeight: FontWeight.w700,
            ),
          ),
          bottom: const TabBar(
            tabs: [
              Tab(text: 'Ca làm'),
              Tab(text: 'Nhiệm vụ'),
              Tab(text: 'Lịch sử'),
            ],
          ),
        ),
        body: TabBarView(
          children: [_shiftTab(), _taskTab(), _historyTab()],
        ),
      ),
    );
  }

  Widget _shiftTab() {
    return RefreshIndicator(
      onRefresh: _loadAssignments,
      child: ListView(
        padding: const EdgeInsets.all(14),
        children: [
          Row(
            children: [
              Expanded(
                child: OutlinedButton.icon(
                  onPressed: _pickDate,
                  icon: const Icon(Icons.calendar_month_outlined),
                  label: Text(_date(selectedDate)),
                ),
              ),
              const SizedBox(width: 8),
              FilledButton.icon(
                onPressed: _assignShift,
                icon: const Icon(Icons.add),
                label: const Text('Phân ca'),
              ),
            ],
          ),
          const SizedBox(height: 14),
          if (loadingAssignments)
            const LinearProgressIndicator()
          else if (assignments.isEmpty)
            _empty('Chưa có phân ca trong ngày này')
          else
            ...assignments.map(
              (a) => Card(
                margin: const EdgeInsets.only(bottom: 10),
                child: ListTile(
                  leading: CircleAvatar(
                    child: Text(
                      a.userName.trim().isEmpty
                          ? '?'
                          : a.userName.trim().substring(0, 1).toUpperCase(),
                    ),
                  ),
                  title: Text(
                    a.userName,
                    style: const TextStyle(fontWeight: FontWeight.w700),
                  ),
                  subtitle: Text(
                    '${a.shiftName} · ${a.shiftTimeLabel}'
                    '${(a.areaName ?? '').trim().isEmpty ? '' : '\nKhu vực: ${a.areaName}'}'
                    '\n${a.statusLabel}',
                  ),
                  trailing: PopupMenuButton<String>(
                    onSelected: (value) {
                      if (value == 's0') _changeAssignmentStatus(a, 0);
                      if (value == 's1') _changeAssignmentStatus(a, 1);
                      if (value == 's2') _changeAssignmentStatus(a, 2);
                      if (value == 's3') _changeAssignmentStatus(a, 3);
                      if (value == 'delete') _deleteAssignment(a);
                    },
                    itemBuilder: (_) => const [
                      PopupMenuItem(value: 's0', child: Text('Đã xếp lịch')),
                      PopupMenuItem(value: 's1', child: Text('Đã điểm danh')),
                      PopupMenuItem(value: 's2', child: Text('Vắng mặt')),
                      PopupMenuItem(value: 's3', child: Text('Đã hoàn thành')),
                      PopupMenuDivider(),
                      PopupMenuItem(value: 'delete', child: Text('Xóa phân ca')),
                    ],
                  ),
                ),
              ),
            ),
        ],
      ),
    );
  }

  Widget _taskTab() {
    return RefreshIndicator(
      onRefresh: _loadTasks,
      child: ListView(
        padding: const EdgeInsets.all(14),
        children: [
          Row(
            children: [
              Expanded(
                child: DropdownButtonFormField<int>(
                  value: taskUserId ?? 0,
                  isExpanded: true,
                  decoration: const InputDecoration(
                    labelText: 'Lọc nhân viên',
                    border: OutlineInputBorder(),
                    isDense: true,
                  ),
                  items: [
                    const DropdownMenuItem<int>(
                      value: 0,
                      child: Text('Tất cả nhân viên'),
                    ),
                    ...employees.map(
                      (e) => DropdownMenuItem<int>(
                        value: e.userId,
                        child: Text(
                          e.fullName,
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                    ),
                  ],
                  onChanged: (v) async {
                    setState(() => taskUserId = (v == null || v == 0) ? null : v);
                    await _loadTasks();
                  },
                ),
              ),
              const SizedBox(width: 8),
              FilledButton.icon(
                onPressed: () => _editTask(),
                icon: const Icon(Icons.add_task_outlined),
                label: const Text('Giao việc'),
              ),
            ],
          ),
          const SizedBox(height: 14),
          if (loadingTasks)
            const LinearProgressIndicator()
          else if (tasks.isEmpty)
            _empty('Chưa có nhiệm vụ')
          else
            ...tasks.map(
              (t) => Card(
                margin: const EdgeInsets.only(bottom: 10),
                child: Padding(
                  padding: const EdgeInsets.fromLTRB(14, 10, 6, 10),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        children: [
                          Expanded(
                            child: Text(
                              t.title,
                              style: const TextStyle(
                                fontWeight: FontWeight.w700,
                                fontSize: 15,
                              ),
                            ),
                          ),
                          PopupMenuButton<String>(
                            onSelected: (v) {
                              if (v == 'edit') _editTask(t);
                              if (v == 'status0') _changeTaskStatus(t, 0);
                              if (v == 'status1') _changeTaskStatus(t, 1);
                              if (v == 'status2') _changeTaskStatus(t, 2);
                              if (v == 'delete') _deleteTask(t);
                            },
                            itemBuilder: (_) => const [
                              PopupMenuItem(
                                value: 'edit',
                                child: Text('Sửa'),
                              ),
                              PopupMenuDivider(),
                              PopupMenuItem(
                                value: 'status0',
                                child: Text('Trạng thái: Chưa làm'),
                              ),
                              PopupMenuItem(
                                value: 'status1',
                                child: Text('Trạng thái: Đang làm'),
                              ),
                              PopupMenuItem(
                                value: 'status2',
                                child: Text('Trạng thái: Hoàn thành'),
                              ),
                              PopupMenuDivider(),
                              PopupMenuItem(
                                value: 'delete',
                                child: Text('Xóa'),
                              ),
                            ],
                          ),
                        ],
                      ),
                      Text(
                        t.assignedToName,
                        style: const TextStyle(
                          color: AppTheme.orangeColor,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                      if ((t.description ?? '').trim().isNotEmpty) ...[
                        const SizedBox(height: 5),
                        Text(t.description!),
                      ],
                      const SizedBox(height: 7),
                      Text(
                        '${t.statusLabel}'
                        '${t.dueAt == null ? '' : ' · Hạn ${_date(t.dueAt!)}'}',
                        style: TextStyle(color: Colors.grey.shade700),
                      ),
                    ],
                  ),
                ),
              ),
            ),
        ],
      ),
    );
  }

  Widget _historyTab() {
    return RefreshIndicator(
      onRefresh: _loadHistory,
      child: ListView(
        padding: const EdgeInsets.all(14),
        children: [
          DropdownButtonFormField<int>(
            value: historyUserId ?? 0,
            isExpanded: true,
            decoration: const InputDecoration(
              labelText: 'Nhân viên',
              border: OutlineInputBorder(),
            ),
            items: [
              const DropdownMenuItem<int>(
                value: 0,
                child: Text('Tất cả nhân viên'),
              ),
              ...employees.map(
                (e) => DropdownMenuItem<int>(
                  value: e.userId,
                  child: Text(e.fullName, overflow: TextOverflow.ellipsis),
                ),
              ),
            ],
            onChanged: (v) async {
              setState(() => historyUserId = (v == null || v == 0) ? null : v);
              await _loadHistory();
            },
          ),
          const SizedBox(height: 10),
          SegmentedButton<int>(
            segments: const [
              ButtonSegment(value: 7, label: Text('7 ngày')),
              ButtonSegment(value: 30, label: Text('30 ngày')),
            ],
            selected: {historyDays},
            onSelectionChanged: (v) async {
              setState(() => historyDays = v.first);
              await _loadHistory();
            },
          ),
          const SizedBox(height: 14),
          if (loadingHistory)
            const LinearProgressIndicator()
          else if (history.isEmpty)
            _empty('Chưa có lịch sử ca làm')
          else
            ...history.map(
              (h) => Card(
                margin: const EdgeInsets.only(bottom: 10),
                child: ListTile(
                  leading: const Icon(Icons.work_history_outlined),
                  title: Text(
                    h.userName,
                    style: const TextStyle(fontWeight: FontWeight.w700),
                  ),
                  subtitle: Text(
                    '${_date(h.workDate)} · ${h.shiftName}'
                    '\n${h.shiftTimeLabel}'
                    '${(h.areaName ?? '').trim().isEmpty ? '' : ' · ${h.areaName}'}'
                    '\n${h.statusLabel}'
                    '${h.checkInTime == null ? '' : ' · Vào ${_time(h.checkInTime!)}'}'
                    '${h.checkOutTime == null ? '' : ' · Ra ${_time(h.checkOutTime!)}'}',
                  ),
                ),
              ),
            ),
        ],
      ),
    );
  }

  Widget _empty(String text) => Padding(
        padding: const EdgeInsets.only(top: 80),
        child: Center(
          child: Text(
            text,
            style: TextStyle(color: Colors.grey.shade600),
          ),
        ),
      );

  static String _date(DateTime v) =>
      '${v.day.toString().padLeft(2, '0')}/${v.month.toString().padLeft(2, '0')}/${v.year}';

  static String _time(DateTime v) =>
      '${v.hour.toString().padLeft(2, '0')}:${v.minute.toString().padLeft(2, '0')}';

  static String _dateTime(DateTime v) => '${_date(v)} ${_time(v)}';
}
