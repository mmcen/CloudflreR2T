#!/usr/bin/perl
# Generate a 256x256 app icon (ICO with uncompressed 32bpp BMP) for R2 Explorer.
# Orange rounded square with a white cloud + accent spark. No external deps.
# Usage: perl generate_icon.pl <output.ico>

use strict;
use warnings;

my $SIZE = 256;

sub clampf { my ($v, $lo, $hi) = @_; return $v < $lo ? $lo : ($v > $hi ? $hi : $v); }

sub sd_round_rect {
    my ($px, $py, $cx, $cy, $hw, $hh, $r) = @_;
    my $qx = abs($px - $cx) - ($hw - $r);
    my $qy = abs($py - $cy) - ($hh - $r);
    my $ax = $qx > 0 ? $qx : 0;
    my $ay = $qy > 0 ? $qy : 0;
    my $outer = sqrt($ax * $ax + $ay * $ay);
    my $inner = ($qx > $qy ? $qx : $qy);
    $inner = $inner < 0 ? $inner : 0;
    return $outer + $inner - $r;
}

sub sd_circle {
    my ($px, $py, $cx, $cy, $r) = @_;
    return sqrt(($px - $cx) ** 2 + ($py - $cy) ** 2) - $r;
}

# alpha composite src over dst; colors are (r,g,b,a) 0..255
sub over {
    my ($dst, $src) = @_;
    my $sa = $src->[3] / 255.0;
    return @$dst if $sa <= 0;
    return @$src if $sa >= 1;
    my $da = $dst->[3] / 255.0;
    my $oa = $sa + $da * (1 - $sa);
    my @out;
    for my $i (0 .. 2) {
        $out[$i] = int(($src->[$i] * $sa + $dst->[$i] * $da * (1 - $sa)) / $oa + 0.5);
    }
    $out[3] = int($oa * 255 + 0.5);
    return @out;
}

my @pix;  # RGBA per pixel

for my $y (0 .. $SIZE - 1) {
    for my $x (0 .. $SIZE - 1) {
        my $px = $x + 0.5;
        my $py = $y + 0.5;

        # Background rounded square (orange vertical gradient), feathered edge
        my $d = sd_round_rect($px, $py, 128, 128, 110, 110, 56);
        my @base;
        if ($d <= 0) {
            my $t = $y / ($SIZE - 1);
            my $r = 247 - (247 - 234) * $t;
            my $g = 163 - (163 - 106) * $t;
            my $b = 60 - (60 - 22) * $t;
            @base = (int($r + 0.5), int($g + 0.5), int($b + 0.5), 255);
        } else {
            my $a = clampf(1.0 - $d, 0, 1);
            @base = (247, 163, 60, int($a * 255 + 0.5));
        }
        my @c = @base;

        # White cloud (three circles + rounded base)
        my $cd = sd_circle($px, $py, 92, 128, 40);
        $cd = sd_circle($px, $py, 140, 98, 48) if $cd > 0;
        $cd = sd_circle($px, $py, 188, 130, 38) if $cd > 0;
        $cd = sd_round_rect($px, $py, 128, 158, 78, 26, 14) if $cd > 0;
        if ($cd <= 0) {
            my $a = clampf(1.0 + $cd, 0, 1);
            @c = over(\@c, [255, 255, 255, int($a * 255 + 0.5)]);
        }

        # Accent spark: soft translucent white circle
        my $sd = sd_circle($px, $py, 188, 172, 16);
        if ($sd <= 0) {
            my $a = clampf(1.0 + $sd, 0, 1) * 0.35;
            @c = over(\@c, [255, 255, 255, int($a * 255 + 0.5)]);
        }

        $pix[$y * $SIZE + $x] = \@c;
    }
}

# Encode as ICO with 32bpp BMP (bottom-up rows, BGRA)
my $row_bytes = $SIZE * 4;
my $bmp_size = $row_bytes * $SIZE;
my $dib = pack("V10", 40, $SIZE, $SIZE * 2, 1, 32, 0, $bmp_size, 0, 0, 0);
my $pixels = "";
for (my $y = $SIZE - 1; $y >= 0; $y--) {
    for my $x (0 .. $SIZE - 1) {
        my $c = $pix[$y * $SIZE + $x];
        $pixels .= pack("C4", $c->[2], $c->[1], $c->[0], $c->[3]);  # B G R A
    }
}

my $image = $dib . $pixels;
my $out = $ARGV[0] || "app.ico";
open(my $fh, ">", $out) or die "cannot write $out: $!";
binmode($fh);
print $fh pack("v3", 0, 1, 1);                      # ICONDIR
print $fh pack("CCCCvvVV", 0, 0, 0, 0, 1, 32, length($image), 22);  # dir entry
print $fh $image;
close($fh);
print "wrote $out (" . (22 + length($image)) . " bytes)\n";
