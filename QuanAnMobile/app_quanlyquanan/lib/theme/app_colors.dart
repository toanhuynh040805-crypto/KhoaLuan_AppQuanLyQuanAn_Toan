import 'package:flutter/material.dart';

// Toan bo mau lay dung tu Design System "Kitchen Precision" (Stitch AI)
// De dong bo, KHONG hardcode ma mau truc tiep trong cac man hinh,
// luon goi qua AppColors.xxx
class AppColors {
  AppColors._();

  // Primary - cam nang luong, dung cho CTA chinh, canh bao don moi
  static const Color primary = Color(0xFFA04100);
  static const Color primaryContainer = Color(0xFFFF6B00);
  static const Color onPrimary = Color(0xFFFFFFFF);
  static const Color onPrimaryContainer = Color(0xFF572000);

  // Secondary - teal sau, dung cho dieu huong, header, phan tu on dinh
  static const Color secondary = Color(0xFF006972);
  static const Color secondaryContainer = Color(0xFF9FF0FB);
  static const Color onSecondary = Color(0xFFFFFFFF);

  // Tertiary - xanh la, dung cho trang thai thanh cong/hoan tat
  static const Color tertiary = Color(0xFF2C694E);
  static const Color tertiaryContainer = Color(0xFF68A586);

  // Semantic
  static const Color error = Color(0xFFBA1A1A);
  static const Color errorContainer = Color(0xFFFFDAD6);
  static const Color onErrorContainer = Color(0xFF93000A);
  static const Color warning = Color(0xFFFF6B00); // dung chung voi primaryContainer

  // Surface / neutral
  static const Color background = Color(0xFFF8F9FA);
  static const Color surfaceContainerLowest = Color(0xFFFFFFFF);
  static const Color surfaceContainerLow = Color(0xFFF3F4F5);
  static const Color surfaceContainer = Color(0xFFEDEEEF);
  static const Color surfaceContainerHigh = Color(0xFFE7E8E9);
  static const Color onSurface = Color(0xFF191C1D);
  static const Color onSurfaceVariant = Color(0xFF5A4136);
  static const Color outline = Color(0xFF8E7164);
  static const Color outlineVariant = Color(0xFFE2BFB0);

  // Trang thai ban an (dung trong TableMapScreen)
  static const Color tableTrongBg = Color(0xFFE7F3EB);
  static const Color tableTrongText = Color(0xFF2C694E);
  static const Color tableDangDungBg = Color(0xFFFDEAE0);
  static const Color tableDangDungText = Color(0xFFA04100);
  static const Color tableDaDatBg = Color(0xFFE7E8E9);
  static const Color tableDaDatText = Color(0xFF5A4136);
}
