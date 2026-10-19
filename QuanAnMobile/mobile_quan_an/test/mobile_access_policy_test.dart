import 'package:flutter_test/flutter_test.dart';
import 'package:mobile_quan_an/services/mobile_access_policy.dart';

void main() {
  group('MobileAccessPolicy', () {
    test('PhucVu has serving permissions but cannot complete payment', () {
      expect(
        MobileAccessPolicy.canManageTables('PhucVu'),
        isTrue,
      );
      expect(
        MobileAccessPolicy.canOrder('Phục vụ'),
        isTrue,
      );
      expect(
        MobileAccessPolicy.canMarkServed('phucvu'),
        isTrue,
      );
      expect(
        MobileAccessPolicy.canRequestPayment('Phục vụ'),
        isTrue,
      );
      expect(
        MobileAccessPolicy.canCompletePayment('Phục vụ'),
        isFalse,
      );
    });

    test('ThuNgan can complete payment but cannot mutate serving workflow', () {
      expect(
        MobileAccessPolicy.canManageTables('ThuNgan'),
        isFalse,
      );
      expect(
        MobileAccessPolicy.canOrder('Thu ngân'),
        isFalse,
      );
      expect(
        MobileAccessPolicy.canCompletePayment('thungan'),
        isTrue,
      );
    });

    test('Bep is read-only for serving workflow', () {
      expect(
        MobileAccessPolicy.canManageTables('Bep'),
        isFalse,
      );
      expect(
        MobileAccessPolicy.canOrder('Bếp'),
        isFalse,
      );
      expect(
        MobileAccessPolicy.canRequestPayment('Bếp'),
        isFalse,
      );
      expect(
        MobileAccessPolicy.canCompletePayment('Bếp'),
        isFalse,
      );
    });

    test('QuanLy and QuanTriVien have full Mobile permissions', () {
      for (final role in <String>[
        'QuanLy',
        'Quản lý',
        'QuanTriVien',
        'Quản trị viên',
        'admin',
      ]) {
        expect(
          MobileAccessPolicy.canManageTables(role),
          isTrue,
        );
        expect(
          MobileAccessPolicy.canOrder(role),
          isTrue,
        );
        expect(
          MobileAccessPolicy.canMarkServed(role),
          isTrue,
        );
        expect(
          MobileAccessPolicy.canRequestPayment(role),
          isTrue,
        );
        expect(
          MobileAccessPolicy.canCompletePayment(role),
          isTrue,
        );
      }
    });

    test('Unknown role is rejected by Mobile access check', () {
      expect(
        MobileAccessPolicy.canUseMobile('UnknownRole'),
        isFalse,
      );
    });
  });
}
