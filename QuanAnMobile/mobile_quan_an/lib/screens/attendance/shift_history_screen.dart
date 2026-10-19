import 'package:flutter/material.dart';
import '../../app/theme.dart';
import '../../models/attendance_state.dart';
import '../../models/employee_profile.dart';
import '../../services/app_services.dart';
import '../../services/api_client.dart';
import '../../widgets/top_message.dart';

enum ShiftHistoryFilter {
  today,
  sevenDays,
  thisMonth,
  all,
}

class ShiftHistoryScreen extends StatefulWidget {
  final AttendanceState attendanceState;

  const ShiftHistoryScreen({
    super.key,
    required this.attendanceState,
  });

  @override
  State<ShiftHistoryScreen> createState() =>
      _ShiftHistoryScreenState();
}

class _ShiftHistoryScreenState
    extends State<ShiftHistoryScreen> {
  ShiftHistoryFilter _selectedFilter =
    ShiftHistoryFilter.today;

  bool _isLoading = true;

  @override
  void initState() {
    super.initState();
    _loadHistory();
  }

  Future<void> _loadHistory() async {
    try {
      await AppServices.attendanceService.loadHistory(
        employeeCode: appEmployee.employeeCode,
        state: widget.attendanceState,
      );

      if (!mounted) return;

      setState(() {
        _isLoading = false;
      });
    } on ApiException catch (error) {
      if (!mounted) return;

      setState(() {
        _isLoading = false;
      });

      TopMessage.show(
        context,
        message: error.message,
        type: TopMessageType.error,
      );
    }
  }

  bool _isSameDay(
    DateTime first,
    DateTime second,
  ) {
    return first.year == second.year &&
        first.month == second.month &&
        first.day == second.day;
  }

  List<ShiftRecord> _getFilteredRecords() {
    final DateTime now = DateTime.now();

    final DateTime today = DateTime(
      now.year,
      now.month,
      now.day,
    );

    final DateTime sevenDaysAgo =
        today.subtract(
      const Duration(days: 6),
    );

    return widget.attendanceState.history.where(
      (record) {
        final DateTime recordDate = DateTime(
          record.date.year,
          record.date.month,
          record.date.day,
        );

        switch (_selectedFilter) {
          case ShiftHistoryFilter.today:
            return _isSameDay(
              recordDate,
              today,
            );

          case ShiftHistoryFilter.sevenDays:
            return !recordDate.isBefore(
                  sevenDaysAgo,
                ) &&
                !recordDate.isAfter(today);

          case ShiftHistoryFilter.thisMonth:
            return recordDate.year ==
                    now.year &&
                recordDate.month ==
                    now.month;

          case ShiftHistoryFilter.all:
            return true;
        }
      },
    ).toList();
  }

  String formatDate(DateTime date) {
    final day =
        date.day.toString().padLeft(2, '0');

    final month =
        date.month.toString().padLeft(2, '0');

    return '$day/$month/${date.year}';
  }

  String formatTime(DateTime time) {
    final hour =
        time.hour.toString().padLeft(2, '0');

    final minute =
        time.minute.toString().padLeft(2, '0');

    return '$hour:$minute';
  }

  String calculateDuration(
    DateTime checkIn,
    DateTime checkOut,
  ) {
    final Duration duration =
        checkOut.difference(checkIn);

    int totalMinutes = duration.inMinutes;

    // Nếu đã qua phút khác nhưng thời gian thực tế
    // chưa đủ 1 phút thì vẫn hiển thị tối thiểu 1 phút
    if (totalMinutes == 0 &&
        checkOut.isAfter(checkIn)) {
      totalMinutes = 1;
    }

    final int hours = totalMinutes ~/ 60;
    final int minutes = totalMinutes % 60;

    return '${hours}h ${minutes}p';
  }

  DateTime _shiftStart(
    ShiftRecord record,
  ) {
    return _shiftDateTime(
      record.date,
      record.startTime,
    );
  }

  DateTime _shiftEnd(
    ShiftRecord record,
  ) {
    return _shiftDateTime(
      record.date,
      record.endTime,
    );
  }

  DateTime _shiftDateTime(
    DateTime date,
    String clock,
  ) {
    final List<String> parts =
        clock.split(':');

    final int hour =
        parts.isNotEmpty
            ? int.tryParse(parts[0]) ?? 0
            : 0;

    final int minute =
        parts.length > 1
            ? int.tryParse(parts[1]) ?? 0
            : 0;

    return DateTime(
      date.year,
      date.month,
      date.day,
      hour,
      minute,
    );
  }

  String _getCheckInStatus(
    ShiftRecord record,
  ) {
    final DateTime start =
        _shiftStart(record);

    if (record.checkInTime.isAfter(start)) {
      final int lateMinutes =
          record.checkInTime
              .difference(start)
              .inMinutes;

      return 'Đi trễ $lateMinutes phút';
    }

    return 'Đúng giờ';
  }

  String _getCheckOutStatus(
    ShiftRecord record,
  ) {
    final DateTime end =
        _shiftEnd(record);

    if (record.checkOutTime.isBefore(end)) {
      final int earlyMinutes =
          end
              .difference(record.checkOutTime)
              .inMinutes;

      return 'Về sớm $earlyMinutes phút';
    }

    return 'Đủ giờ';
  }

  Color _getCheckInColor(
    ShiftRecord record,
  ) {
    final DateTime start =
        _shiftStart(record);

    return record.checkInTime.isAfter(start)
        ? Colors.red
        : Colors.green;
  }

  Color _getCheckOutColor(
    ShiftRecord record,
  ) {
    final DateTime end =
        _shiftEnd(record);

    return record.checkOutTime.isBefore(end)
        ? Colors.orange
        : Colors.green;
  }

  Widget _buildStatusBadge({
    required String text,
    required Color color,
  }) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: 9,
        vertical: 5,
      ),
      decoration: BoxDecoration(
        color: color.withValues(
          alpha: 0.10,
        ),
        borderRadius:
            BorderRadius.circular(10),
      ),
      child: Text(
        text,
        style: TextStyle(
          color: color,
          fontSize: 11,
          fontWeight: FontWeight.w600,
        ),
      ),
    );
  }

  int _getTotalShifts() {
    return _getFilteredRecords().length;
  }

  int _getOnTimeShifts() {
    return _getFilteredRecords().where(
      (record) {
        final DateTime start =
            _shiftStart(record);

        return !record.checkInTime
            .isAfter(start);
      },
    ).length;
  }

  int _getLateShifts() {
    return _getFilteredRecords().where(
      (record) {
        final DateTime start =
            _shiftStart(record);

        return record.checkInTime
            .isAfter(start);
      },
    ).length;
  }

  int _getTotalWorkMinutes() {
    int totalMinutes = 0;

    for (final record
        in _getFilteredRecords()) {
      final Duration duration =
          record.checkOutTime.difference(
        record.checkInTime,
      );

      int minutes = duration.inMinutes;

      // Đồng bộ với calculateDuration()
      // Có thời gian làm nhưng chưa đủ 1 phút
      // thì vẫn tính tối thiểu 1 phút
      if (minutes == 0 &&
          record.checkOutTime.isAfter(
            record.checkInTime,
          )) {
        minutes = 1;
      }

      totalMinutes += minutes;
    }

    return totalMinutes;
  }

  String _formatTotalDuration() {
    final int totalMinutes =
        _getTotalWorkMinutes();

    final int hours =
        totalMinutes ~/ 60;

    final int minutes =
        totalMinutes % 60;

    return '${hours}h ${minutes}p';
  }

  Widget _buildSummaryItem({
    required IconData icon,
    required String title,
    required String value,
  }) {
    return Expanded(
      child: Container(
        padding: const EdgeInsets.symmetric(
          vertical: 14,
          horizontal: 8,
        ),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius:
              BorderRadius.circular(12),
          border: Border.all(
            color: Colors.grey.shade200,
          ),
        ),
        child: Column(
          children: [
            Icon(
              icon,
              size: 23,
              color: AppTheme.orangeColor,
            ),

            const SizedBox(height: 7),

            Text(
              value,
              style: const TextStyle(
                fontSize: 17,
                fontWeight: FontWeight.bold,
              ),
            ),

            const SizedBox(height: 3),

            Text(
              title,
              textAlign: TextAlign.center,
              style: TextStyle(
                fontSize: 11,
                color: Colors.grey.shade600,
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildAttendanceSummary() {
    return Column(
      crossAxisAlignment:
          CrossAxisAlignment.start,
      children: [
        const Text(
          'Tổng quan chấm công',
          style: TextStyle(
            fontSize: 16,
            fontWeight: FontWeight.bold,
          ),
        ),

        const SizedBox(height: 10),

        // =========================================
        // HÀNG 1
        // =========================================
        Row(
          children: [
            _buildSummaryItem(
              icon:
                  Icons.calendar_month_outlined,
              title: 'Tổng số ca',
              value:
                  '${_getTotalShifts()}',
            ),

            const SizedBox(width: 10),

            _buildSummaryItem(
              icon:
                  Icons.check_circle_outline,
              title: 'Đúng giờ',
              value:
                  '${_getOnTimeShifts()}',
            ),
          ],
        ),

        const SizedBox(height: 10),

        // =========================================
        // HÀNG 2
        // =========================================
        Row(
          children: [
            _buildSummaryItem(
              icon: Icons.schedule_outlined,
              title: 'Đi trễ',
              value:
                  '${_getLateShifts()}',
            ),

            const SizedBox(width: 10),

            _buildSummaryItem(
              icon:
                  Icons.exit_to_app_outlined,
              title: 'Về sớm',
              value:
                  '${_getEarlyLeaveShifts()}',
            ),
          ],
        ),

        const SizedBox(height: 10),

        // =========================================
        // TỔNG THỜI GIAN
        // =========================================
        Container(
          width: double.infinity,
          padding: const EdgeInsets.symmetric(
            horizontal: 16,
            vertical: 14,
          ),

          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius:
                BorderRadius.circular(12),

            border: Border.all(
              color: Colors.grey.shade200,
            ),
          ),

          child: Row(
            children: [
              Container(
                width: 42,
                height: 42,

                decoration: const BoxDecoration(
                  color: Color(0xFFFFF1E8),
                  shape: BoxShape.circle,
                ),

                child: const Icon(
                  Icons.timelapse_outlined,
                  color: AppTheme.orangeColor,
                ),
              ),

              const SizedBox(width: 12),

              const Expanded(
                child: Text(
                  'Tổng thời gian làm',
                  style: TextStyle(
                    fontSize: 14,
                    fontWeight:
                        FontWeight.w600,
                  ),
                ),
              ),

              Text(
                _formatTotalDuration(),
                style: const TextStyle(
                  fontSize: 18,
                  fontWeight:
                      FontWeight.bold,
                  color:
                      AppTheme.orangeColor,
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }

  int _getEarlyLeaveShifts() {
    return _getFilteredRecords().where(
      (record) {
        final DateTime end =
            _shiftEnd(record);

        return record.checkOutTime
            .isBefore(end);
      },
    ).length;
  }

  Widget _buildFilterChip({
    required String label,
    required ShiftHistoryFilter filter,
  }) {
    final bool selected =
        _selectedFilter == filter;

    return ChoiceChip(
      label: Text(label),

      selected: selected,

      onSelected: (_) {
        setState(() {
          _selectedFilter = filter;
        });
      },

      showCheckmark: false,

      selectedColor:
          const Color(0xFFFFE9DE),

      backgroundColor:
          Colors.white,

      side: BorderSide(
        color: selected
            ? AppTheme.orangeColor
            : Colors.grey.shade300,
      ),

      labelStyle: TextStyle(
        color: selected
            ? AppTheme.orangeColor
            : Colors.grey.shade700,

        fontWeight: selected
            ? FontWeight.w600
            : FontWeight.normal,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final List<ShiftRecord> records =
      _getFilteredRecords();

    return Scaffold(
      backgroundColor:
          const Color(0xFFF8F8F8),

      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0,

        leading: IconButton(
          onPressed: () {
            Navigator.pop(context);
          },
          icon: const Icon(
            Icons.arrow_back,
            color: AppTheme.textColor,
          ),
        ),

        title: const Text(
          'Lịch sử ca làm',
          style: TextStyle(
            color: AppTheme.textColor,
            fontSize: 20,
            fontWeight: FontWeight.bold,
          ),
        ),
      ),

      body: _isLoading
          ? const Center(
              child: CircularProgressIndicator(),
            )
          : RefreshIndicator(
              onRefresh: _loadHistory,
              child: SingleChildScrollView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.all(16),

        child: Column(
          crossAxisAlignment:
              CrossAxisAlignment.start,

          children: [
            // =========================================
            // TỔNG QUAN CHẤM CÔNG
            // =========================================
            _buildAttendanceSummary(),

            const SizedBox(height: 18),

            // =========================================
            // BỘ LỌC
            // =========================================
            SingleChildScrollView(
              scrollDirection: Axis.horizontal,

              child: Row(
                children: [
                  _buildFilterChip(
                    label: 'Hôm nay',
                    filter:
                        ShiftHistoryFilter.today,
                  ),

                  const SizedBox(width: 8),

                  _buildFilterChip(
                    label: '7 ngày',
                    filter:
                        ShiftHistoryFilter.sevenDays,
                  ),

                  const SizedBox(width: 8),

                  _buildFilterChip(
                    label: 'Tháng này',
                    filter:
                        ShiftHistoryFilter.thisMonth,
                  ),

                  const SizedBox(width: 8),

                  _buildFilterChip(
                    label: 'Tất cả',
                    filter:
                        ShiftHistoryFilter.all,
                  ),
                ],
              ),
            ),

            const SizedBox(height: 24),

            // =========================================
            // DANH SÁCH CA LÀM
            // =========================================
            const Text(
              'Danh sách ca làm',
              style: TextStyle(
                fontSize: 16,
                fontWeight: FontWeight.bold,
              ),
            ),

            const SizedBox(height: 10),

            if (records.isEmpty)
              Container(
                width: double.infinity,
                padding: const EdgeInsets.all(28),

                decoration: BoxDecoration(
                  color: Colors.white,
                  borderRadius:
                      BorderRadius.circular(14),

                  border: Border.all(
                    color: Colors.grey.shade200,
                  ),
                ),

                child: const Column(
                  children: [
                    Icon(
                      Icons.history,
                      size: 52,
                      color: Colors.grey,
                    ),

                    SizedBox(height: 12),

                    Text(
                      'Chưa có lịch sử ca làm',
                      style: TextStyle(
                        fontSize: 15,
                        color: Colors.grey,
                      ),
                    ),
                  ],
                ),
              )
            else
              ListView.separated(
                shrinkWrap: true,

                physics:
                    const NeverScrollableScrollPhysics(),

                itemCount: records.length,

                separatorBuilder:
                    (context, index) {
                  return const SizedBox(
                    height: 12,
                  );
                },

                itemBuilder:
                    (context, index) {
                  final record =
                      records[index];

                  return Container(
                    padding:
                        const EdgeInsets.all(16),

                    decoration: BoxDecoration(
                      color: Colors.white,

                      borderRadius:
                          BorderRadius.circular(
                        14,
                      ),

                      border: Border.all(
                        color:
                            Colors.grey.shade200,
                      ),
                    ),

                    child: Column(
                      crossAxisAlignment:
                          CrossAxisAlignment.start,

                      children: [
                        // =============================
                        // HEADER CA LÀM
                        // =============================
                        Row(
                          children: [
                            Container(
                              width: 42,
                              height: 42,

                              decoration:
                                  BoxDecoration(
                                color:
                                    const Color(
                                  0xFFDDF1E7,
                                ),

                                borderRadius:
                                    BorderRadius
                                        .circular(
                                  21,
                                ),
                              ),

                              child: const Icon(
                                Icons
                                    .work_history_outlined,
                                color:
                                    Colors.green,
                              ),
                            ),

                            const SizedBox(
                              width: 12,
                            ),

                            Expanded(
                              child: Column(
                                crossAxisAlignment:
                                    CrossAxisAlignment
                                        .start,

                                children: [
                                  Text(
                                    formatDate(
                                      record.date,
                                    ),

                                    style:
                                        const TextStyle(
                                      fontSize: 16,
                                      fontWeight:
                                          FontWeight
                                              .bold,
                                    ),
                                  ),

                                  const SizedBox(
                                    height: 3,
                                  ),

                                  Text(
                                    record.shiftName,

                                    style:
                                        TextStyle(
                                      fontSize: 12,
                                      color: Colors
                                          .grey
                                          .shade600,
                                    ),
                                  ),
                                ],
                              ),
                            ),

                            Container(
                              padding:
                                  const EdgeInsets
                                      .symmetric(
                                horizontal: 10,
                                vertical: 5,
                              ),

                              decoration:
                                  BoxDecoration(
                                color: Colors
                                    .green
                                    .shade50,

                                borderRadius:
                                    BorderRadius
                                        .circular(
                                  14,
                                ),
                              ),

                              child:
                                  const Text(
                                'Hoàn thành',
                                style:
                                    TextStyle(
                                  color:
                                      Colors.green,
                                  fontSize: 11,
                                  fontWeight:
                                      FontWeight
                                          .w600,
                                ),
                              ),
                            ),
                          ],
                        ),

                        const SizedBox(
                          height: 16,
                        ),

                        const Divider(
                          height: 1,
                        ),

                        const SizedBox(
                          height: 14,
                        ),

                        // =============================
                        // GIỜ VÀO / GIỜ RA
                        // =============================
                        Row(
                          children: [
                            Expanded(
                              child:
                                  _buildTimeItem(
                                title:
                                    'Giờ vào',
                                value:
                                    formatTime(
                                  record
                                      .checkInTime,
                                ),
                                icon:
                                    Icons.login,
                              ),
                            ),

                            Expanded(
                              child:
                                  _buildTimeItem(
                                title:
                                    'Giờ ra',
                                value:
                                    formatTime(
                                  record
                                      .checkOutTime,
                                ),
                                icon:
                                    Icons.logout,
                              ),
                            ),
                          ],
                        ),

                        const SizedBox(
                          height: 14,
                        ),

                        // =============================
                        // THỜI GIAN LÀM
                        // =============================
                        Container(
                          width:
                              double.infinity,

                          padding:
                              const EdgeInsets
                                  .symmetric(
                            horizontal: 12,
                            vertical: 10,
                          ),

                          decoration:
                              BoxDecoration(
                            color:
                                const Color(
                              0xFFFFF1E8,
                            ),

                            borderRadius:
                                BorderRadius
                                    .circular(
                              8,
                            ),
                          ),

                          child: Row(
                            children: [
                              const Icon(
                                Icons
                                    .access_time,
                                size: 18,
                                color: AppTheme
                                    .orangeColor,
                              ),

                              const SizedBox(
                                width: 8,
                              ),

                              const Text(
                                'Thời gian làm:',
                                style:
                                    TextStyle(
                                  fontSize: 13,
                                ),
                              ),

                              const Spacer(),

                              Text(
                                calculateDuration(
                                  record
                                      .checkInTime,
                                  record
                                      .checkOutTime,
                                ),

                                style:
                                    const TextStyle(
                                  color: AppTheme
                                      .orangeColor,

                                  fontSize: 13,

                                  fontWeight:
                                      FontWeight
                                          .bold,
                                ),
                              ),
                            ],
                          ),
                        ),

                        // =============================
                        // TRẠNG THÁI ĐIỂM DANH
                        // =============================
                        const SizedBox(
                          height: 14,
                        ),

                        const Divider(
                          height: 1,
                        ),

                        const SizedBox(
                          height: 12,
                        ),

                        Row(
                          crossAxisAlignment:
                              CrossAxisAlignment
                                  .start,

                          children: [
                            Expanded(
                              child: Column(
                                crossAxisAlignment:
                                    CrossAxisAlignment
                                        .start,

                                children: [
                                  Text(
                                    'Trạng thái vào',
                                    style:
                                        TextStyle(
                                      fontSize: 12,
                                      color: Colors
                                          .grey
                                          .shade600,
                                    ),
                                  ),

                                  const SizedBox(
                                    height: 6,
                                  ),

                                  _buildStatusBadge(
                                    text:
                                        _getCheckInStatus(
                                      record,
                                    ),
                                    color:
                                        _getCheckInColor(
                                      record,
                                    ),
                                  ),
                                ],
                              ),
                            ),

                            const SizedBox(
                              width: 12,
                            ),

                            Expanded(
                              child: Column(
                                crossAxisAlignment:
                                    CrossAxisAlignment
                                        .start,

                                children: [
                                  Text(
                                    'Trạng thái ra',
                                    style:
                                        TextStyle(
                                      fontSize: 12,
                                      color: Colors
                                          .grey
                                          .shade600,
                                    ),
                                  ),

                                  const SizedBox(
                                    height: 6,
                                  ),

                                  _buildStatusBadge(
                                    text:
                                        _getCheckOutStatus(
                                      record,
                                    ),
                                    color:
                                        _getCheckOutColor(
                                      record,
                                    ),
                                  ),
                                ],
                              ),
                            ),
                          ],
                        ),
                      ],
                    ),
                  );
                },
              ),
          ],
        ),
      ),
            ),
    );
  }

  Widget _buildTimeItem({
    required String title,
    required String value,
    required IconData icon,
  }) {
    return Row(
      children: [
        Icon(
          icon,
          size: 19,
          color: AppTheme.orangeColor,
        ),

        const SizedBox(width: 8),

        Column(
          crossAxisAlignment:
              CrossAxisAlignment.start,

          children: [
            Text(
              title,
              style: TextStyle(
                fontSize: 12,
                color: Colors.grey.shade600,
              ),
            ),

            const SizedBox(height: 3),

            Text(
              value,
              style: const TextStyle(
                fontSize: 14,
                fontWeight:
                    FontWeight.w600,
              ),
            ),
          ],
        ),
      ],
    );
  }
}