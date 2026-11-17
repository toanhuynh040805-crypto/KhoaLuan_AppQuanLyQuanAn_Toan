import 'package:flutter/material.dart';

import '../login/login_screen.dart';
import 'server_setup_screen.dart';

class ServerBootstrapScreen extends StatelessWidget {
  const ServerBootstrapScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return ServerSetupScreen(
      autoDiscover: true,
      allowBack: false,
      onConnected: () {
        Navigator.of(context).pushReplacement(
          MaterialPageRoute<void>(
            builder: (_) => const LoginScreen(),
          ),
        );
      },
    );
  }
}
