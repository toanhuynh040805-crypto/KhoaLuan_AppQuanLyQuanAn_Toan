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
  List<ManagedRole> roles = [];
  List<ManagedStation> stations = [];
  List<ManagedShift> shifts = [];
  List<ManagedArea> areas = [];
  List<ManagedShiftAssignment> assignments = [];
  List<ManagedStaffTask> tasks = [];
  List<ManagedShiftAssignment> history = [];

  DateTime selectedDate = DateTime.now();
  int? taskUserId;
  int? historyUserId;
  int historyDays = 7;
  DateTime? historySpecificDate;
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
        AppServices.staffManagementService.getRoles(),
        AppServices.staffManagementService.getStations(),
        AppServices.staffManagementService.getShifts(),
        AppServices.staffManagementService.getAreas(),
      ]);
      if (!mounted) return;
      setState(() {
        employees = result[0] as List<ManagedEmployee>;
        roles = result[1] as List<ManagedRole>;
        stations = result[2] as List<ManagedStation>;
        shifts = result[3] as List<ManagedShift>;
        areas = result[4] as List<ManagedArea>;
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
    if (mounted) {
      setState(() => loadingHistory = true);
    }

    final DateTime to;
    final DateTime from;

    if (historySpecificDate != null) {
      final DateTime day =
          historySpecificDate!;
      from = DateTime(
        day.year,
        day.month,
        day.day,
      );
      to = from;
    } else {
      to = DateTime.now();
      from = to.subtract(
        Duration(days: historyDays - 1),
      );
    }

    try {
      final rows =
          await AppServices
              .staffManagementService
              .getHistory(
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

      setState(() {
        loadingHistory = false;
      });

      _show(
        e.message,
        TopMessageType.error,
      );
    }
  }

  Future<void> _pickHistoryDate() async {
    final DateTime initial =
        historySpecificDate ??
            DateTime.now();

    final DateTime? picked =
        await showDatePicker(
      context: context,
      initialDate: initial,
      firstDate: DateTime.now().subtract(
        const Duration(days: 730),
      ),
      lastDate: DateTime.now().add(
        const Duration(days: 730),
      ),
    );

    if (picked == null || !mounted) {
      return;
    }

    setState(() {
      historySpecificDate = picked;
    });

    await _loadHistory();
  }

  String get _historyEmployeeLabel {
    if (historyUserId == null) {
      return 'Tất cả nhân viên';
    }

    for (final employee in employees) {
      if (employee.userId ==
          historyUserId) {
        return employee.fullName;
      }
    }

    return 'Nhân viên';
  }

  void _show(String message, TopMessageType type) {
    if (!mounted) return;
    TopMessage.show(context, message: message, type: type);
  }

  Future<void> _reloadEmployees() async {
    try {
      final rows =
          await AppServices.staffManagementService
              .getEmployees();

      if (!mounted) return;

      setState(() {
        employees = rows;
      });
    } on ApiException catch (e) {
      _show(e.message, TopMessageType.error);
    }
  }

  String _suggestEmployeeCode() {
    int maxNumber = 0;

    for (final employee in employees) {
      final String digits =
          employee.employeeCode.replaceAll(
        RegExp(r'\D'),
        '',
      );

      final int? value =
          int.tryParse(digits);

      if (value != null && value > maxNumber) {
        maxNumber = value;
      }
    }

    return 'NV' +
        (maxNumber + 1)
            .toString()
            .padLeft(3, '0');
  }

  Future<void> _addEmployee() async {
    if (roles.isEmpty) {
      _show(
        'Chưa có dữ liệu vai trò để tạo nhân viên.',
        TopMessageType.warning,
      );
      return;
    }

    final fullName = TextEditingController();
    final employeeCode = TextEditingController(
      text: _suggestEmployeeCode(),
    );
    final username = TextEditingController();
    final password = TextEditingController();
    final confirmPassword =
        TextEditingController();
    final phone = TextEditingController();
    final email = TextEditingController();

    ManagedRole role = roles.firstWhere(
      (item) =>
          item.name.trim().toLowerCase() ==
          'phucvu',
      orElse: () => roles.first,
    );
    ManagedStation? station;
    bool obscurePassword = true;
    bool obscureConfirmPassword = true;
    bool submitting = false;

    final bool? saved =
        await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      builder: (sheetContext) =>
          StatefulBuilder(
        builder: (context, setSheetState) {
          final bool needsStation =
              role.isKitchen;

          return Padding(
            padding: EdgeInsets.fromLTRB(
              18,
              4,
              18,
              MediaQuery.viewInsetsOf(context)
                      .bottom +
                  20,
            ),
            child: SingleChildScrollView(
              child: Column(
                mainAxisSize:
                    MainAxisSize.min,
                crossAxisAlignment:
                    CrossAxisAlignment.start,
                children: [
                  const Text(
                    'Thêm nhân viên',
                    style: TextStyle(
                      fontSize: 20,
                      fontWeight:
                          FontWeight.w700,
                    ),
                  ),
                  const SizedBox(height: 6),
                  Text(
                    'Tạo đồng thời tài khoản đăng nhập và hồ sơ nhân viên.',
                    style: TextStyle(
                      fontSize: 12,
                      color:
                          Colors.grey.shade700,
                    ),
                  ),
                  const SizedBox(height: 16),
                  TextField(
                    controller: fullName,
                    textCapitalization:
                        TextCapitalization.words,
                    decoration:
                        const InputDecoration(
                      labelText:
                          'Họ và tên *',
                      border:
                          OutlineInputBorder(),
                      prefixIcon:
                          Icon(
                        Icons.person_outline,
                      ),
                    ),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller:
                        employeeCode,
                    textCapitalization:
                        TextCapitalization
                            .characters,
                    decoration:
                        const InputDecoration(
                      labelText:
                          'Mã nhân viên *',
                      border:
                          OutlineInputBorder(),
                      prefixIcon:
                          Icon(
                        Icons.badge_outlined,
                      ),
                    ),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: username,
                    autocorrect: false,
                    enableSuggestions: false,
                    decoration:
                        const InputDecoration(
                      labelText:
                          'Tên đăng nhập *',
                      border:
                          OutlineInputBorder(),
                      prefixIcon:
                          Icon(
                        Icons
                            .account_circle_outlined,
                      ),
                    ),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: password,
                    obscureText:
                        obscurePassword,
                    autocorrect: false,
                    enableSuggestions: false,
                    decoration:
                        InputDecoration(
                      labelText:
                          'Mật khẩu *',
                      helperText:
                          'Tối thiểu 6 ký tự',
                      border:
                          const OutlineInputBorder(),
                      prefixIcon:
                          const Icon(
                        Icons.lock_outline,
                      ),
                      suffixIcon:
                          IconButton(
                        onPressed: () {
                          setSheetState(() {
                            obscurePassword =
                                !obscurePassword;
                          });
                        },
                        icon: Icon(
                          obscurePassword
                              ? Icons
                                  .visibility_outlined
                              : Icons
                                  .visibility_off_outlined,
                        ),
                      ),
                    ),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller:
                        confirmPassword,
                    obscureText:
                        obscureConfirmPassword,
                    autocorrect: false,
                    enableSuggestions: false,
                    decoration:
                        InputDecoration(
                      labelText:
                          'Nhập lại mật khẩu *',
                      border:
                          const OutlineInputBorder(),
                      prefixIcon:
                          const Icon(
                        Icons
                            .lock_reset_outlined,
                      ),
                      suffixIcon:
                          IconButton(
                        onPressed: () {
                          setSheetState(() {
                            obscureConfirmPassword =
                                !obscureConfirmPassword;
                          });
                        },
                        icon: Icon(
                          obscureConfirmPassword
                              ? Icons
                                  .visibility_outlined
                              : Icons
                                  .visibility_off_outlined,
                        ),
                      ),
                    ),
                  ),
                  const SizedBox(height: 12),
                  DropdownButtonFormField<
                      ManagedRole>(
                    value: role,
                    isExpanded: true,
                    decoration:
                        const InputDecoration(
                      labelText:
                          'Vai trò *',
                      border:
                          OutlineInputBorder(),
                      prefixIcon:
                          Icon(
                        Icons
                            .admin_panel_settings_outlined,
                      ),
                    ),
                    items: roles
                        .map(
                          (item) =>
                              DropdownMenuItem<
                                  ManagedRole>(
                            value: item,
                            child: Text(
                              item.name,
                              overflow:
                                  TextOverflow
                                      .ellipsis,
                            ),
                          ),
                        )
                        .toList(),
                    onChanged: (value) {
                      if (value == null) return;

                      setSheetState(() {
                        role = value;

                        if (!role.isKitchen) {
                          station = null;
                        }
                      });
                    },
                  ),
                  if (needsStation) ...[
                    const SizedBox(height: 12),
                    DropdownButtonFormField<
                        ManagedStation>(
                      value: station,
                      isExpanded: true,
                      decoration:
                          const InputDecoration(
                        labelText:
                            'Bếp / Quầy nước *',
                        border:
                            OutlineInputBorder(),
                        prefixIcon:
                            Icon(
                          Icons
                              .soup_kitchen_outlined,
                        ),
                      ),
                      items: stations
                          .map(
                            (item) =>
                                DropdownMenuItem<
                                    ManagedStation>(
                              value: item,
                              child: Text(
                                item.name,
                                overflow:
                                    TextOverflow
                                        .ellipsis,
                              ),
                            ),
                          )
                          .toList(),
                      onChanged: (value) {
                        setSheetState(() {
                          station = value;
                        });
                      },
                    ),
                    if (stations.isEmpty) ...[
                      const SizedBox(height: 6),
                      Text(
                        'Chưa có Bếp/Quầy nước. Hãy cấu hình bộ phận trước khi tạo tài khoản Bếp.',
                        style: TextStyle(
                          color:
                              Colors.red.shade700,
                          fontSize: 12,
                        ),
                      ),
                    ],
                  ],
                  const SizedBox(height: 12),
                  TextField(
                    controller: phone,
                    keyboardType:
                        TextInputType.phone,
                    decoration:
                        const InputDecoration(
                      labelText:
                          'Số điện thoại',
                      border:
                          OutlineInputBorder(),
                      prefixIcon:
                          Icon(
                        Icons.phone_outlined,
                      ),
                    ),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: email,
                    keyboardType:
                        TextInputType
                            .emailAddress,
                    autocorrect: false,
                    decoration:
                        const InputDecoration(
                      labelText: 'Email',
                      border:
                          OutlineInputBorder(),
                      prefixIcon:
                          Icon(
                        Icons.email_outlined,
                      ),
                    ),
                  ),
                  const SizedBox(height: 18),
                  SizedBox(
                    width: double.infinity,
                    height: 50,
                    child: FilledButton.icon(
                      onPressed: submitting
                          ? null
                          : () async {
                              final String name =
                                  fullName.text
                                      .trim();
                              final String code =
                                  employeeCode
                                      .text
                                      .trim()
                                      .toUpperCase();
                              final String login =
                                  username.text
                                      .trim();
                              final String pass =
                                  password.text;
                              final String confirm =
                                  confirmPassword
                                      .text;

                              if (name.isEmpty ||
                                  code.isEmpty ||
                                  login.isEmpty) {
                                TopMessage.show(
                                  sheetContext,
                                  message:
                                      'Vui lòng nhập đủ họ tên, mã nhân viên và tên đăng nhập.',
                                  type:
                                      TopMessageType
                                          .warning,
                                );
                                return;
                              }

                              if (pass.length < 6) {
                                TopMessage.show(
                                  sheetContext,
                                  message:
                                      'Mật khẩu phải có ít nhất 6 ký tự.',
                                  type:
                                      TopMessageType
                                          .warning,
                                );
                                return;
                              }

                              if (pass != confirm) {
                                TopMessage.show(
                                  sheetContext,
                                  message:
                                      'Mật khẩu nhập lại không khớp.',
                                  type:
                                      TopMessageType
                                          .warning,
                                );
                                return;
                              }

                              if (role.isKitchen &&
                                  station == null) {
                                TopMessage.show(
                                  sheetContext,
                                  message:
                                      'Tài khoản Bếp phải chọn Bếp hoặc Quầy nước.',
                                  type:
                                      TopMessageType
                                          .warning,
                                );
                                return;
                              }

                              final String emailText =
                                  email.text.trim();

                              if (emailText
                                      .isNotEmpty &&
                                  (!emailText
                                          .contains('@') ||
                                      !emailText
                                          .contains('.'))) {
                                TopMessage.show(
                                  sheetContext,
                                  message:
                                      'Email chưa đúng định dạng.',
                                  type:
                                      TopMessageType
                                          .warning,
                                );
                                return;
                              }

                              setSheetState(() {
                                submitting = true;
                              });

                              try {
                                await AppServices
                                    .staffManagementService
                                    .createEmployee(
                                  fullName: name,
                                  employeeCode:
                                      code,
                                  username:
                                      login,
                                  password:
                                      pass,
                                  roleId:
                                      role.id,
                                  stationId:
                                      role.isKitchen
                                          ? station
                                              ?.id
                                          : null,
                                  phone:
                                      phone.text,
                                  email:
                                      emailText,
                                );

                                if (sheetContext
                                    .mounted) {
                                  Navigator.pop(
                                    sheetContext,
                                    true,
                                  );
                                }
                              } on ApiException catch (e) {
                                if (sheetContext
                                    .mounted) {
                                  setSheetState(
                                    () {
                                      submitting =
                                          false;
                                    },
                                  );

                                  TopMessage.show(
                                    sheetContext,
                                    message:
                                        e.message,
                                    type:
                                        TopMessageType
                                            .error,
                                  );
                                }
                              }
                            },
                      icon: submitting
                          ? const SizedBox(
                              width: 18,
                              height: 18,
                              child:
                                  CircularProgressIndicator(
                                strokeWidth: 2,
                                color:
                                    Colors.white,
                              ),
                            )
                          : const Icon(
                              Icons
                                  .person_add_alt_1,
                            ),
                      label: Text(
                        submitting
                            ? 'Đang tạo...'
                            : 'Tạo nhân viên',
                      ),
                    ),
                  ),
                ],
              ),
            ),
          );
        },
      ),
    );

    // Không dispose controller tại đây: Future của modal có thể hoàn tất
    // trong lúc animation đóng BottomSheet vẫn còn render vài frame cuối.
    // Dispose ngay lúc này có thể làm TextField đọc controller đã dispose
    // và kích hoạt ErrorWidget toàn màn hình.
    if (saved == true) {
      // Chờ BottomSheet hoàn tất frame đóng trước khi reload danh sách.
      await Future<void>.delayed(
        const Duration(milliseconds: 250),
      );

      if (!mounted) return;

      await _reloadEmployees();

      if (!mounted) return;

      _show(
        'Đã tạo nhân viên mới',
        TopMessageType.success,
      );
    }
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
    final active =
        employees
            .where((e) => e.isActive)
            .toList();

    if (active.isEmpty ||
        shifts.isEmpty) {
      _show(
        'Cần có nhân viên đang hoạt động và ca làm trước khi phân ca.',
        TopMessageType.warning,
      );
      return;
    }

    ManagedEmployee employee =
        active.first;
    ManagedShift shift = shifts.first;
    final Set<int> selectedAreaIds =
        <int>{};

    final saved =
        await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      builder: (sheetContext) =>
          StatefulBuilder(
        builder: (context, setSheetState) =>
            Padding(
          padding: EdgeInsets.fromLTRB(
            18,
            4,
            18,
            MediaQuery.viewInsetsOf(
                      context,
                    ).bottom +
                20,
          ),
          child: SingleChildScrollView(
            child: Column(
              mainAxisSize:
                  MainAxisSize.min,
              crossAxisAlignment:
                  CrossAxisAlignment.start,
              children: [
                const Text(
                  'Phân ca nhân viên',
                  style: TextStyle(
                    fontSize: 20,
                    fontWeight:
                        FontWeight.w700,
                  ),
                ),
                const SizedBox(height: 16),
                DropdownButtonFormField<
                    ManagedEmployee>(
                  value: employee,
                  isExpanded: true,
                  decoration:
                      const InputDecoration(
                    labelText: 'Nhân viên',
                    border:
                        OutlineInputBorder(),
                  ),
                  items: active
                      .map(
                        (e) =>
                            DropdownMenuItem(
                          value: e,
                          child: Text(
                            e.fullName,
                            overflow:
                                TextOverflow
                                    .ellipsis,
                          ),
                        ),
                      )
                      .toList(),
                  onChanged: (value) {
                    if (value == null) {
                      return;
                    }

                    setSheetState(() {
                      employee = value;
                    });
                  },
                ),
                const SizedBox(height: 12),
                DropdownButtonFormField<
                    ManagedShift>(
                  value: shift,
                  isExpanded: true,
                  decoration:
                      const InputDecoration(
                    labelText: 'Ca làm',
                    border:
                        OutlineInputBorder(),
                  ),
                  items: shifts
                      .map(
                        (item) =>
                            DropdownMenuItem(
                          value: item,
                          child: Text(
                            '${item.name} · ${item.timeLabel}',
                            overflow:
                                TextOverflow
                                    .ellipsis,
                          ),
                        ),
                      )
                      .toList(),
                  onChanged: (value) {
                    if (value == null) {
                      return;
                    }

                    setSheetState(() {
                      shift = value;
                    });
                  },
                ),
                const SizedBox(height: 16),
                const Text(
                  'Khu vực phụ trách',
                  style: TextStyle(
                    fontSize: 14,
                    fontWeight:
                        FontWeight.w700,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  'Có thể chọn nhiều khu cho cùng một ca. Không chọn khu nào nếu chưa muốn phân khu.',
                  style: TextStyle(
                    fontSize: 12,
                    height: 1.35,
                    color:
                        Colors.grey.shade700,
                  ),
                ),
                const SizedBox(height: 8),
                if (areas.isEmpty)
                  Text(
                    'Chưa có khu vực để phân công.',
                    style: TextStyle(
                      color:
                          Colors.grey.shade700,
                    ),
                  )
                else
                  Container(
                    decoration: BoxDecoration(
                      border: Border.all(
                        color:
                            Colors.grey.shade300,
                      ),
                      borderRadius:
                          BorderRadius.circular(
                        10,
                      ),
                    ),
                    child: Column(
                      children: areas
                          .map(
                            (area) =>
                                CheckboxListTile(
                              dense: true,
                              value:
                                  selectedAreaIds
                                      .contains(
                                area.id,
                              ),
                              title:
                                  Text(area.name),
                              controlAffinity:
                                  ListTileControlAffinity
                                      .leading,
                              onChanged:
                                  (checked) {
                                setSheetState(
                                  () {
                                    if (checked ==
                                        true) {
                                      selectedAreaIds
                                          .add(
                                        area.id,
                                      );
                                    } else {
                                      selectedAreaIds
                                          .remove(
                                        area.id,
                                      );
                                    }
                                  },
                                );
                              },
                            ),
                          )
                          .toList(),
                    ),
                  ),
                const SizedBox(height: 16),
                SizedBox(
                  width: double.infinity,
                  height: 48,
                  child: FilledButton.icon(
                    onPressed: () async {
                      try {
                        await AppServices
                            .staffManagementService
                            .assignShift(
                          userId:
                              employee.userId,
                          shiftId: shift.id,
                          workDate:
                              selectedDate,
                          areaIds:
                              selectedAreaIds
                                  .toList(),
                        );

                        if (sheetContext
                            .mounted) {
                          Navigator.pop(
                            sheetContext,
                            true,
                          );
                        }
                      } on ApiException catch (e) {
                        if (sheetContext
                            .mounted) {
                          TopMessage.show(
                            sheetContext,
                            message:
                                e.message,
                            type:
                                TopMessageType
                                    .error,
                          );
                        }
                      }
                    },
                    icon: const Icon(
                      Icons
                          .event_available_outlined,
                    ),
                    label: Text(
                      'Phân ca ngày ${_date(selectedDate)}',
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );

    if (saved == true) {
      await _loadAssignments();
      await _loadHistory();

      _show(
        'Đã phân ca cho nhân viên',
        TopMessageType.success,
      );
    }
  }

  Future<void> _editAssignmentAreas(
    ManagedShiftAssignment assignment,
  ) async {
    final Set<int> selectedAreaIds =
        assignment.areaIds.isNotEmpty
            ? assignment.areaIds.toSet()
            : assignment.areaId == null
                ? <int>{}
                : <int>{
                    assignment.areaId!,
                  };

    final bool? saved =
        await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      builder: (sheetContext) =>
          StatefulBuilder(
        builder: (context, setSheetState) =>
            Padding(
          padding: EdgeInsets.fromLTRB(
            18,
            4,
            18,
            MediaQuery.viewInsetsOf(
                      context,
                    ).bottom +
                20,
          ),
          child: SingleChildScrollView(
            child: Column(
              mainAxisSize:
                  MainAxisSize.min,
              crossAxisAlignment:
                  CrossAxisAlignment.start,
              children: [
                Text(
                  'Khu vực · ${assignment.userName}',
                  style: const TextStyle(
                    fontSize: 20,
                    fontWeight:
                        FontWeight.w700,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  '${assignment.shiftName} · ${assignment.shiftTimeLabel} · ${_date(assignment.workDate)}',
                  style: TextStyle(
                    color:
                        Colors.grey.shade700,
                    fontSize: 12,
                  ),
                ),
                const SizedBox(height: 14),
                ...areas.map(
                  (area) =>
                      CheckboxListTile(
                    contentPadding:
                        EdgeInsets.zero,
                    value: selectedAreaIds
                        .contains(area.id),
                    title: Text(area.name),
                    controlAffinity:
                        ListTileControlAffinity
                            .leading,
                    onChanged: (checked) {
                      setSheetState(() {
                        if (checked == true) {
                          selectedAreaIds
                              .add(area.id);
                        } else {
                          selectedAreaIds
                              .remove(area.id);
                        }
                      });
                    },
                  ),
                ),
                const SizedBox(height: 12),
                SizedBox(
                  width: double.infinity,
                  height: 48,
                  child: FilledButton.icon(
                    onPressed: () async {
                      try {
                        await AppServices
                            .staffManagementService
                            .updateAssignmentAreas(
                          id: assignment.id,
                          areaIds:
                              selectedAreaIds
                                  .toList(),
                        );

                        if (sheetContext
                            .mounted) {
                          Navigator.pop(
                            sheetContext,
                            true,
                          );
                        }
                      } on ApiException catch (e) {
                        if (sheetContext
                            .mounted) {
                          TopMessage.show(
                            sheetContext,
                            message:
                                e.message,
                            type:
                                TopMessageType
                                    .error,
                          );
                        }
                      }
                    },
                    icon: const Icon(
                      Icons.save_outlined,
                    ),
                    label: const Text(
                      'Lưu khu vực',
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );

    if (saved == true) {
      await _loadAssignments();
      await _loadHistory();

      _show(
        'Đã cập nhật khu vực của ca',
        TopMessageType.success,
      );
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
      length: 4,
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
            isScrollable: true,
            tabs: [
              Tab(text: 'Nhân viên'),
              Tab(text: 'Ca làm'),
              Tab(text: 'Nhiệm vụ'),
              Tab(text: 'Lịch sử'),
            ],
          ),
        ),
        body: TabBarView(
          children: [
            _employeeTab(),
            _shiftTab(),
            _taskTab(),
            _historyTab(),
          ],
        ),
      ),
    );
  }

  Widget _employeeTab() {
    final int activeCount =
        employees.where((e) => e.isActive).length;

    return RefreshIndicator(
      onRefresh: _reloadEmployees,
      child: ListView(
        padding: const EdgeInsets.all(14),
        children: [
          Row(
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment:
                      CrossAxisAlignment.start,
                  children: [
                    const Text(
                      'Danh sách nhân viên',
                      style: TextStyle(
                        fontSize: 17,
                        fontWeight:
                            FontWeight.w700,
                      ),
                    ),
                    const SizedBox(height: 3),
                    Text(
                      '${employees.length} nhân viên • $activeCount đang hoạt động',
                      style: TextStyle(
                        fontSize: 12,
                        color:
                            Colors.grey.shade700,
                      ),
                    ),
                  ],
                ),
              ),
              FilledButton.icon(
                onPressed: _addEmployee,
                icon: const Icon(
                  Icons.person_add_alt_1,
                ),
                label: const Text('Thêm'),
              ),
            ],
          ),
          const SizedBox(height: 14),
          if (employees.isEmpty)
            _empty(
              'Chưa có nhân viên. Bấm Thêm để tạo nhân viên đầu tiên.',
            )
          else
            ...employees.map(
              (employee) => Card(
                margin:
                    const EdgeInsets.only(
                  bottom: 10,
                ),
                child: ListTile(
                  leading: CircleAvatar(
                    backgroundColor:
                        employee.isActive
                            ? const Color(
                                0xFFE7F6EC,
                              )
                            : Colors
                                .grey.shade200,
                    child: Text(
                      employee.fullName
                              .trim()
                              .isEmpty
                          ? '?'
                          : employee.fullName
                              .trim()
                              .substring(0, 1)
                              .toUpperCase(),
                      style: TextStyle(
                        color: employee
                                .isActive
                            ? Colors.green
                            : Colors.grey,
                        fontWeight:
                            FontWeight.w700,
                      ),
                    ),
                  ),
                  title: Text(
                    employee.fullName,
                    style: const TextStyle(
                      fontWeight:
                          FontWeight.w700,
                    ),
                  ),
                  subtitle: Text(
                    '${employee.employeeCode} • ${employee.roleName}\n'
                    '${employee.isActive ? 'Đang hoạt động' : 'Tạm khóa'}',
                  ),
                  isThreeLine: true,
                  trailing: Container(
                    padding:
                        const EdgeInsets.symmetric(
                      horizontal: 9,
                      vertical: 5,
                    ),
                    decoration: BoxDecoration(
                      color: employee.isActive
                          ? Colors.green.withValues(
                              alpha: 0.10,
                            )
                          : Colors.grey.withValues(
                              alpha: 0.12,
                            ),
                      borderRadius:
                          BorderRadius.circular(12),
                    ),
                    child: Text(
                      employee.isActive
                          ? 'Hoạt động'
                          : 'Tạm khóa',
                      style: TextStyle(
                        fontSize: 11,
                        fontWeight:
                            FontWeight.w600,
                        color: employee
                                .isActive
                            ? Colors.green
                            : Colors
                                .grey.shade700,
                      ),
                    ),
                  ),
                ),
              ),
            ),
        ],
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
                    '${a.areaLabel.isEmpty ? '' : '\nKhu vực: ${a.areaLabel}'}'
                    '\n${a.statusLabel}',
                  ),
                  trailing: PopupMenuButton<String>(
                    onSelected: (value) {
                      if (value == 'areas') {
                        _editAssignmentAreas(a);
                      }
                      if (value == 's0') _changeAssignmentStatus(a, 0);
                      if (value == 's1') _changeAssignmentStatus(a, 1);
                      if (value == 's2') _changeAssignmentStatus(a, 2);
                      if (value == 's3') _changeAssignmentStatus(a, 3);
                      if (value == 'delete') _deleteAssignment(a);
                    },
                    itemBuilder: (_) => const [
                      PopupMenuItem(
                        value: 'areas',
                        child: Text('Sửa khu vực'),
                      ),
                      PopupMenuDivider(),
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
    final DateTime? specificDate =
        historySpecificDate;

    return RefreshIndicator(
      onRefresh: _loadHistory,
      child: ListView(
        padding: const EdgeInsets.all(14),
        children: [
          DropdownButtonFormField<int>(
            value: historyUserId ?? 0,
            isExpanded: true,
            decoration:
                const InputDecoration(
              labelText: 'Nhân viên',
              border:
                  OutlineInputBorder(),
            ),
            items: [
              const DropdownMenuItem<int>(
                value: 0,
                child: Text(
                  'Tất cả nhân viên',
                ),
              ),
              ...employees.map(
                (e) =>
                    DropdownMenuItem<int>(
                  value: e.userId,
                  child: Text(
                    e.fullName,
                    overflow:
                        TextOverflow
                            .ellipsis,
                  ),
                ),
              ),
            ],
            onChanged: (value) async {
              setState(() {
                historyUserId =
                    value == null ||
                            value == 0
                        ? null
                        : value;
              });

              await _loadHistory();
            },
          ),
          const SizedBox(height: 10),
          SegmentedButton<int>(
            segments: const [
              ButtonSegment(
                value: 7,
                label: Text('7 ngày'),
              ),
              ButtonSegment(
                value: 30,
                label: Text('30 ngày'),
              ),
            ],
            selected: {historyDays},
            onSelectionChanged:
                (value) async {
              setState(() {
                historyDays =
                    value.first;
                historySpecificDate =
                    null;
              });

              await _loadHistory();
            },
          ),
          const SizedBox(height: 10),
          Row(
            children: [
              Expanded(
                child:
                    OutlinedButton.icon(
                  onPressed:
                      _pickHistoryDate,
                  icon: const Icon(
                    Icons
                        .calendar_month_outlined,
                  ),
                  label: Text(
                    specificDate == null
                        ? 'Chọn ngày cụ thể'
                        : 'Ngày ${_date(specificDate)}',
                  ),
                ),
              ),
              if (specificDate !=
                  null) ...[
                const SizedBox(width: 8),
                IconButton.outlined(
                  tooltip:
                      'Bỏ lọc ngày',
                  onPressed: () async {
                    setState(() {
                      historySpecificDate =
                          null;
                    });

                    await _loadHistory();
                  },
                  icon: const Icon(
                    Icons.close,
                  ),
                ),
              ],
            ],
          ),
          if (specificDate != null) ...[
            const SizedBox(height: 12),
            Container(
              padding:
                  const EdgeInsets.all(
                12,
              ),
              decoration: BoxDecoration(
                color:
                    const Color(
                  0xFFFFF0E8,
                ),
                borderRadius:
                    BorderRadius.circular(
                  12,
                ),
              ),
              child: Row(
                children: [
                  const Icon(
                    Icons
                        .work_history_outlined,
                    color:
                        AppTheme.orangeColor,
                  ),
                  const SizedBox(
                    width: 10,
                  ),
                  Expanded(
                    child: Text(
                      '$_historyEmployeeLabel · ${_date(specificDate)}',
                      style:
                          const TextStyle(
                        fontWeight:
                            FontWeight.w600,
                      ),
                    ),
                  ),
                  Text(
                    '${history.length} ca',
                    style:
                        const TextStyle(
                      color:
                          AppTheme.orangeColor,
                      fontWeight:
                          FontWeight.w800,
                    ),
                  ),
                ],
              ),
            ),
          ],
          const SizedBox(height: 14),
          if (loadingHistory)
            const LinearProgressIndicator()
          else if (history.isEmpty)
            _empty(
              specificDate == null
                  ? 'Chưa có lịch sử ca làm'
                  : 'Không có ca làm trong ngày ${_date(specificDate)}',
            )
          else
            ...history.map(
              (h) => Card(
                margin:
                    const EdgeInsets.only(
                  bottom: 10,
                ),
                child: ListTile(
                  leading: const Icon(
                    Icons
                        .work_history_outlined,
                  ),
                  title: Text(
                    h.userName,
                    style:
                        const TextStyle(
                      fontWeight:
                          FontWeight.w700,
                    ),
                  ),
                  subtitle: Text(
                    '${_date(h.workDate)} · ${h.shiftName}'
                    '\n${h.shiftTimeLabel}'
                    '${h.areaLabel.isEmpty ? '' : ' · ${h.areaLabel}'}'
                    '\n${h.punctualityLabel}'
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
