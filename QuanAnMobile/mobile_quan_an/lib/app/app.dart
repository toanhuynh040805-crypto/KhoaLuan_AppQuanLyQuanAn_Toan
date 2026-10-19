import 'package:flutter/material.dart';
import '../screens/login/login_screen.dart';
import 'theme.dart';

class RestaurantApp extends StatelessWidget {
  const RestaurantApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      debugShowCheckedModeBanner: false,
      title: 'GustoPOS Mobile',
      theme: AppTheme.lightTheme,
      home: const LoginScreen(),
    );
  }
}