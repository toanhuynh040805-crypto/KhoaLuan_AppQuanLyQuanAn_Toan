import 'package:flutter/material.dart';
import 'theme/app_theme.dart';
import 'screens/login_screen.dart';

void main() {
  runApp(const QuanAnApp());
}

class QuanAnApp extends StatelessWidget {
  const QuanAnApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Quan An - Phuc Vu',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.light,
      home: const LoginScreen(),
    );
  }
}
